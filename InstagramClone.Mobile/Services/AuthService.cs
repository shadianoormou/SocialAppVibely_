using System.Net.Http.Json;
using System.Text.Json;
using InstagramClone.Mobile.Models;
using Microsoft.Maui.Authentication;

namespace InstagramClone.Mobile.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;

    public AuthService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(GetBaseAddress()),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<AuthResponse?> LoginAsync(
        string emailOrUserName,
        string password)
    {
        var request = new LoginRequest
        {
            EmailOrUserName = emailOrUserName,
            Password = password
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            var message = TryReadErrorMessage(errorContent);

            throw new InvalidOperationException(message);
        }

        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/register",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(TryReadErrorMessage(errorContent));
        }

        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<PhoneCodeResponse?> RequestPhoneCodeAsync(string phoneNumber)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/phone/request-code",
            new { PhoneNumber = phoneNumber });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        return await response.Content.ReadFromJsonAsync<PhoneCodeResponse>();
    }

    public async Task<AuthResponse?> VerifyPhoneCodeAsync(string phoneNumber, string code)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/phone/verify-code",
            new { PhoneNumber = phoneNumber, Code = code });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<AuthResponse?> AuthenticateExternalAsync(string provider)
    {
        var startUrl = new Uri(_httpClient.BaseAddress!, $"api/auth/{provider}/start");
        var callbackUrl = new Uri("vibely://auth");
        var result = await WebAuthenticator.Default.AuthenticateAsync(startUrl, callbackUrl);
        if (!result.Properties.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(result.Properties.TryGetValue("error", out var error)
                ? error
                : "The provider did not return a Vibely session.");

        return new AuthResponse
        {
            Token = token,
            UserId = result.Properties.TryGetValue("userId", out var userId) && int.TryParse(userId, out var parsedUserId)
                ? parsedUserId
                : 0,
            UserName = result.Properties.TryGetValue("userName", out var userName) ? userName : string.Empty
        };
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        return TryReadErrorMessage(errorContent);
    }

    private static string TryReadErrorMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "Login failed. Please try again.";
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty(
                    "message",
                    out var messageElement))
            {
                return messageElement.GetString()
                    ?? "Login failed.";
            }
        }
        catch (JsonException)
        {
          
        }

        return "Invalid username/email or password.";
    }

    private static string GetBaseAddress()
    {
#if ANDROID
        return "http://10.0.2.2:5112/";
#else
        return "http://127.0.0.1:5112/";
#endif
    }
}
