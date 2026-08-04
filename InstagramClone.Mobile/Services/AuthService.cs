using System.Net.Http.Json;
using System.Text.Json;
using InstagramClone.Mobile.Models;

namespace InstagramClone.Mobile.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;

    public AuthService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:5112/"),
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
}