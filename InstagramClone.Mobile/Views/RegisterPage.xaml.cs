using InstagramClone.Mobile.Models;
using InstagramClone.Mobile.Services;

namespace InstagramClone.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _authService = new();

    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void BackButton_Clicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }

    private async void CreateAccountButton_Clicked(object? sender, EventArgs e)
    {
        var fullName = FullNameEntry.Text?.Trim() ?? string.Empty;
        var username = UserNameEntry.Text?.Trim() ?? string.Empty;
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) || password.Length < 6)
        {
            await DisplayAlertAsync("Almost there", "Fill every field and use a password with at least 6 characters.", "OK");
            return;
        }

        SetLoading(true);
        try
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                FullName = fullName,
                UserName = username,
                Email = email,
                Password = password
            });

            if (result is null || string.IsNullOrWhiteSpace(result.Token))
                throw new InvalidOperationException("The server returned an invalid response.");

            await SecureStorage.Default.SetAsync("auth_token", result.Token);
            await SecureStorage.Default.SetAsync("user_id", result.UserId.ToString());
            await SecureStorage.Default.SetAsync("user_name", result.UserName);
            await Shell.Current.GoToAsync("//MainPage");
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync("Connection error", "The Vibely API is not running or the API address is incorrect.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Could not create account", ex.Message, "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void PhoneRegisterButton_Clicked(object? sender, EventArgs e)
    {
        PhoneRegisterButton.IsEnabled = false;
        try
        {
            var phoneNumber = await DisplayPromptAsync(
                "Create with phone",
                "Enter your number in international format, for example +8801XXXXXXXXX.",
                "Send code",
                "Cancel",
                keyboard: Keyboard.Telephone);
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return;

            var challenge = await _authService.RequestPhoneCodeAsync(phoneNumber.Trim());
            var code = await DisplayPromptAsync(
                "Verify your number",
                challenge?.DevelopmentCode is null
                    ? "Enter the 6-digit code sent to your phone."
                    : $"Development mode code: {challenge.DevelopmentCode}",
                "Create account",
                "Cancel",
                keyboard: Keyboard.Numeric,
                maxLength: 6);
            if (string.IsNullOrWhiteSpace(code))
                return;

            var result = await _authService.VerifyPhoneCodeAsync(phoneNumber.Trim(), code.Trim());
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
                throw new InvalidOperationException("The server returned an invalid response.");

            await SecureStorage.Default.SetAsync("auth_token", result.Token);
            await SecureStorage.Default.SetAsync("user_id", result.UserId.ToString());
            await SecureStorage.Default.SetAsync("user_name", result.UserName);
            await Shell.Current.GoToAsync("//MainPage");
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync("Connection error", "The Vibely API is not running or the API address is incorrect.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Phone sign-up failed", ex.Message, "OK");
        }
        finally
        {
            PhoneRegisterButton.IsEnabled = true;
        }
    }

    private void SetLoading(bool loading)
    {
        CreateAccountButton.IsEnabled = !loading;
        FullNameEntry.IsEnabled = !loading;
        UserNameEntry.IsEnabled = !loading;
        EmailEntry.IsEnabled = !loading;
        PasswordEntry.IsEnabled = !loading;
        CreateAccountButton.Text = loading ? "Creating your space…" : "Create account";
        RegisterActivityIndicator.IsVisible = loading;
        RegisterActivityIndicator.IsRunning = loading;
    }
}
