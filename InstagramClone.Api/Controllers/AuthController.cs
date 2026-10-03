using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using InstagramClone.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly PasswordHasher<User> _passwordHasher = new();
    private static readonly HttpClient HttpClient = new();
    private static readonly ConcurrentDictionary<string, OAuthState> OAuthStates = new();

    public AuthController(
        AppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUserName = request.UserName.Trim().ToLowerInvariant();

        var emailExists = await _dbContext.Users
            .AnyAsync(x => x.Email == normalizedEmail);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email already exists."
            });
        }

        var userNameExists = await _dbContext.Users
            .AnyAsync(x => x.UserName == normalizedUserName);

        if (userNameExists)
        {
            return Conflict(new
            {
                message = "Username already exists."
            });
        }

        var user = new User
        {
                UserName = normalizedUserName,
                Email = normalizedEmail,
                FullName = request.FullName.Trim(),
                PhoneNumber = null
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return Ok(CreateAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request)
    {
        var loginValue = request.EmailOrUserName
            .Trim()
            .ToLowerInvariant();

        var user = await _dbContext.Users.FirstOrDefaultAsync(x =>
            x.Email == loginValue ||
            x.UserName == loginValue);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Invalid credentials."
            });
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid credentials."
            });
        }

        return Ok(CreateAuthResponse(user));
    }

    [HttpPost("phone/request-code")]
    public async Task<ActionResult> RequestPhoneCode(RequestPhoneCodeRequest request)
    {
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        if (phoneNumber is null)
            return BadRequest(new { message = "Use an international number, for example +8801XXXXXXXXX." });

        var recentChallenge = await _dbContext.PhoneOtpChallenges
            .AnyAsync(x => x.PhoneNumber == phoneNumber &&
                          x.CreatedAtUtc > DateTime.UtcNow.AddSeconds(-60));
        if (recentChallenge)
            return StatusCode(429, new { message = "Please wait a minute before requesting another code." });

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        _dbContext.PhoneOtpChallenges.Add(new PhoneOtpChallenge
        {
            PhoneNumber = phoneNumber,
            CodeHash = HashCode(code),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        });
        await _dbContext.SaveChangesAsync();

        var delivered = await SendSmsAsync(phoneNumber, $"Your Vibely verification code is {code}. It expires in 5 minutes.");
        if (!delivered && !_environment.IsDevelopment())
            return StatusCode(503, new { message = "Phone verification is not configured on this server." });

        return Ok(new
        {
            message = delivered ? "Verification code sent." : "Development verification code generated.",
            expiresInSeconds = 300,
            developmentCode = _environment.IsDevelopment() ? code : null
        });
    }

    [HttpPost("phone/verify-code")]
    public async Task<ActionResult<AuthResponse>> VerifyPhoneCode(VerifyPhoneCodeRequest request)
    {
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        if (phoneNumber is null)
            return BadRequest(new { message = "Invalid phone number." });

        var challenge = await _dbContext.PhoneOtpChallenges
            .Where(x => x.PhoneNumber == phoneNumber &&
                        x.ConsumedAtUtc == null &&
                        x.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (challenge is null)
            return BadRequest(new { message = "Your code has expired. Request a new one." });

        challenge.Attempts++;
        if (challenge.Attempts > 5)
            return BadRequest(new { message = "Too many attempts. Request a new code." });

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(challenge.CodeHash),
                Convert.FromHexString(HashCode(request.Code))))
        {
            await _dbContext.SaveChangesAsync();
            return BadRequest(new { message = "That verification code is incorrect." });
        }

        challenge.ConsumedAtUtc = DateTime.UtcNow;
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber);
        if (user is null)
        {
            var suffix = phoneNumber[^4..];
            var userName = $"vibely_{suffix}";
            var index = 1;
            while (await _dbContext.Users.AnyAsync(x => x.UserName == userName))
                userName = $"vibely_{suffix}_{index++}";

            user = new User
            {
                UserName = userName,
                Email = $"{Guid.NewGuid():N}@phone.vibely.local",
                FullName = "Vibely member",
                PhoneNumber = phoneNumber
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
            _dbContext.Users.Add(user);
        }

        await _dbContext.SaveChangesAsync();
        return Ok(CreateAuthResponse(user));
    }

    private async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        var accountSid = _configuration["Twilio:AccountSid"];
        var authToken = _configuration["Twilio:AuthToken"];
        var fromNumber = _configuration["Twilio:FromNumber"];
        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(fromNumber))
            return false;

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = phoneNumber,
            ["From"] = fromNumber,
            ["Body"] = message
        });
        using var response = await client.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private static string? NormalizePhoneNumber(string phoneNumber)
    {
        var normalized = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (phoneNumber.TrimStart().StartsWith('+'))
            normalized = "+" + normalized;
        return normalized.StartsWith('+') && normalized.Length is >= 9 and <= 16
            ? normalized
            : null;
    }

    private static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    [HttpGet("{provider}/start")]
    public ActionResult StartExternalLogin(string provider)
    {
        var providerName = provider.Trim().ToLowerInvariant();
        if (providerName is not ("google" or "facebook"))
            return NotFound(new { message = "This sign-in provider is not supported." });

        var providerKey = char.ToUpperInvariant(providerName[0]) + providerName[1..];
        var clientId = _configuration[$"OAuth:{providerKey}:ClientId"];
        var authorizationEndpoint = _configuration[$"OAuth:{providerKey}:AuthorizationEndpoint"];
        var redirectUri = _configuration[$"OAuth:{providerKey}:RedirectUri"];
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/{providerName}/callback";
        }
        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(authorizationEndpoint) ||
            string.IsNullOrWhiteSpace(redirectUri))
            return StatusCode(503, new { message = $"{providerKey} OAuth is not configured on this server." });

        var state = Guid.NewGuid().ToString("N");
        var isWebClient = string.Equals(
            Request.Query["client"],
            "web",
            StringComparison.OrdinalIgnoreCase);
        OAuthStates[state] = new OAuthState(
            providerName,
            redirectUri,
            isWebClient,
            DateTime.UtcNow.AddMinutes(10));
        var scope = providerName == "google" ? "openid email profile" : "email public_profile";
        var authorizationUrl = $"{authorizationEndpoint}?" + string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = state
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return Request.Query.ContainsKey("check")
            ? Ok(new { authorizationUrl })
            : Redirect(authorizationUrl);
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> ExternalCallback(string provider, string? code, string? state, string? error)
    {
        var providerName = provider.Trim().ToLowerInvariant();
        if (error is not null)
        {
            if (state is not null && OAuthStates.TryRemove(state, out var failedState))
                return OAuthErrorRedirect(failedState, error);
            return Redirect($"vibely://auth?error={Uri.EscapeDataString(error)}");
        }
        if (code is null ||
            state is null ||
            !OAuthStates.TryRemove(state, out var oauthState) ||
            oauthState.Provider != providerName ||
            oauthState.ExpiresAtUtc < DateTime.UtcNow)
            return Redirect("vibely://auth?error=invalid_oauth_state");

        try
        {
            using var tokenDocument = await ExchangeCodeAsync(code, providerName, oauthState.RedirectUri);
            if (tokenDocument is null || !tokenDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement))
                return OAuthErrorRedirect(oauthState, "oauth_exchange_failed");

            var accessToken = accessTokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
                return OAuthErrorRedirect(oauthState, "oauth_token_missing");

            var profile = await FetchExternalProfileAsync(providerName, accessToken);
            if (profile is null || string.IsNullOrWhiteSpace(profile.Email))
                return OAuthErrorRedirect(oauthState, "provider_email_missing");

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == profile.Email);
            if (user is null)
            {
                var baseUserName = NormalizeUserName(profile.UserName ?? profile.Email.Split('@')[0]);
                var userName = baseUserName;
                var index = 1;
                while (await _dbContext.Users.AnyAsync(x => x.UserName == userName))
                    userName = $"{baseUserName}_{index++}";
                user = new User
                {
                    UserName = userName,
                    Email = profile.Email,
                    FullName = profile.FullName ?? userName,
                    ProfileImageUrl = profile.ProfileImageUrl,
                    PhoneNumber = null
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
                _dbContext.Users.Add(user);
            }

            await _dbContext.SaveChangesAsync();
            var auth = CreateAuthResponse(user);
            return oauthState.IsWebClient
                ? Redirect($"{GetWebClientBaseUrl()}/#oauth=success&token={Uri.EscapeDataString(auth.Token)}&userId={auth.UserId}&userName={Uri.EscapeDataString(auth.UserName)}")
                : Redirect($"vibely://auth?token={Uri.EscapeDataString(auth.Token)}&userId={auth.UserId}&userName={Uri.EscapeDataString(auth.UserName)}");
        }
        catch
        {
            return OAuthErrorRedirect(oauthState, "oauth_exchange_failed");
        }
    }

    private IActionResult OAuthErrorRedirect(OAuthState oauthState, string error)
    {
        return oauthState.IsWebClient
            ? Redirect($"{GetWebClientBaseUrl()}/#oauth=error&message={Uri.EscapeDataString(error)}")
            : Redirect($"vibely://auth?error={Uri.EscapeDataString(error)}");
    }

    private string GetWebClientBaseUrl()
    {
        var configured = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var renderHost = _configuration["RENDER_EXTERNAL_HOSTNAME"];
        return string.IsNullOrWhiteSpace(renderHost)
            ? "http://127.0.0.1:8765"
            : $"https://{renderHost}";
    }

    private async Task<JsonDocument?> ExchangeCodeAsync(
        string code,
        string provider,
        string redirectUri)
    {
        var providerKey = char.ToUpperInvariant(provider[0]) + provider[1..];
        var endpoint = _configuration[$"OAuth:{providerKey}:TokenEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _configuration[$"OAuth:{providerKey}:ClientId"] ?? string.Empty,
            ["client_secret"] = _configuration[$"OAuth:{providerKey}:ClientSecret"] ?? string.Empty,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        });
        using var response = await HttpClient.PostAsync(endpoint, content);
        if (!response.IsSuccessStatusCode)
            return null;
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<ExternalProfile?> FetchExternalProfileAsync(string provider, string accessToken)
    {
        if (provider == "google")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            return new ExternalProfile(
                root.GetProperty("email").GetString() ?? string.Empty,
                root.TryGetProperty("name", out var name) ? name.GetString() : null,
                root.TryGetProperty("picture", out var picture) ? picture.GetString() : null,
                root.TryGetProperty("email", out var email) ? email.GetString()?.Split('@')[0] : null);
        }

        var url = $"https://graph.facebook.com/v20.0/me?fields=id,name,email,picture.type(large)&access_token={Uri.EscapeDataString(accessToken)}";
        using var facebookResponse = await HttpClient.GetAsync(url);
        facebookResponse.EnsureSuccessStatusCode();
        using var facebookDocument = JsonDocument.Parse(await facebookResponse.Content.ReadAsStringAsync());
        var facebookRoot = facebookDocument.RootElement;
        var facebookEmail = facebookRoot.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
        var image = facebookRoot.TryGetProperty("picture", out var pictureElement) &&
                    pictureElement.TryGetProperty("data", out var pictureData) &&
                    pictureData.TryGetProperty("url", out var pictureUrl)
            ? pictureUrl.GetString()
            : null;
        return new ExternalProfile(
            facebookEmail ?? string.Empty,
            facebookRoot.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null,
            image,
            null);
    }

    private static string NormalizeUserName(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "vibely_member" : normalized[..Math.Min(normalized.Length, 45)];
    }

    private sealed record ExternalProfile(string Email, string? FullName, string? ProfileImageUrl, string? UserName);
    private sealed record OAuthState(string Provider, string RedirectUri, bool IsWebClient, DateTime ExpiresAtUtc);

    private AuthResponse CreateAuthResponse(User user)
    {
        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new(
                JwtRegisteredClaimNames.UniqueName,
                user.UserName),

            new(
                JwtRegisteredClaimNames.Email,
                user.Email),

            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString())
        };

        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key was not configured.");

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var expiryMinutes =
            _configuration.GetValue<int>("Jwt:ExpiryMinutes");

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new AuthResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Token = new JwtSecurityTokenHandler()
                .WriteToken(token)
        };
    }
}
