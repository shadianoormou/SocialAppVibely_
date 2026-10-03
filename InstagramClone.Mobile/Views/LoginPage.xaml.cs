using InstagramClone.Mobile.Models;
using InstagramClone.Mobile.Services;

namespace InstagramClone.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService = new();

    private bool _isPasswordVisible;
    private bool _hasAnimated;

    public LoginPage()
    {
        InitializeComponent();
    }

    private IReadOnlyList<VisualElement> WordmarkCharacters =>
    [
        LetterV,
        LetterI,
        LetterB,
        LetterE,
        LetterL,
        LetterY
    ];

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasAnimated)
        {
            return;
        }

        _hasAnimated = true;

        BrandSection.Opacity = 0;
        BrandSection.TranslationY = -24;

        BrandLogo.Opacity = 0;
        BrandLogo.Scale = 0.72;
        BrandLogo.Rotation = -10;

        LoginCard.Opacity = 0;
        LoginCard.Scale = 0.95;
        LoginCard.TranslationY = 28;

        WordmarkStroke.WidthRequest = 0;
        WordmarkStroke.Opacity = 0;

        foreach (var character in WordmarkCharacters)
        {
            character.Opacity = 0;
            character.Scale = 0.75;
        }

        await Task.WhenAll(
            BrandSection.FadeToAsync(
                1,
                450,
                Easing.CubicOut),

            BrandSection.TranslateToAsync(
                0,
                0,
                550,
                Easing.CubicOut),

            BrandLogo.FadeToAsync(
                1,
                450,
                Easing.CubicOut),

            BrandLogo.ScaleToAsync(
                1,
                650,
                Easing.SpringOut),

            BrandLogo.RotateToAsync(
                0,
                650,
                Easing.CubicOut)
        );

        await AnimateWordmarkAsync();

        await Task.WhenAll(
            LoginCard.FadeToAsync(
                1,
                600,
                Easing.CubicOut),

            LoginCard.ScaleToAsync(
                1,
                600,
                Easing.CubicOut),

            LoginCard.TranslateToAsync(
                0,
                0,
                600,
                Easing.CubicOut)
        );
    }

    private async Task AnimateWordmarkAsync()
    {
        foreach (var character in WordmarkCharacters)
        {
            await Task.WhenAll(
                character.FadeToAsync(
                    1,
                    180,
                    Easing.CubicOut),

                character.TranslateToAsync(
                    0,
                    0,
                    260,
                    Easing.SpringOut),

                character.ScaleToAsync(
                    1,
                    260,
                    Easing.SpringOut),

                character.RotateToAsync(
                    0,
                    260,
                    Easing.CubicOut)
            );

            await Task.Delay(35);
        }

        WordmarkStroke.WidthRequest = 126;

        await WordmarkStroke.FadeToAsync(
            1,
            420,
            Easing.CubicInOut);
    }

    private async void LoginButton_Clicked(
        object? sender,
        EventArgs e)
    {
        var emailOrUserName = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(emailOrUserName) ||
            string.IsNullOrWhiteSpace(password))
        {
            await LoginCard.ScaleToAsync(
                0.98,
                90,
                Easing.CubicOut);

            await LoginCard.ScaleToAsync(
                1,
                120,
                Easing.SpringOut);

            await DisplayAlertAsync(
                "Missing information",
                "Enter your email or username and password.",
                "OK");

            return;
        }

        SetLoadingState(true);

        try
        {
            var result = await _authService.LoginAsync(
                emailOrUserName,
                password);

            if (result is null ||
                string.IsNullOrWhiteSpace(result.Token))
            {
                throw new InvalidOperationException(
                    "The server returned an invalid response.");
            }

            await LoginButton.ScaleToAsync(
                0.96,
                90,
                Easing.CubicOut);

            await LoginButton.ScaleToAsync(
                1,
                130,
                Easing.SpringOut);

            await CompleteLoginAsync(result);
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync(
                "Connection error",
                "The Vibely API is not running or the API address is incorrect.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                "Login failed",
                ex.Message,
                "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void PasswordToggleButton_Clicked(
        object? sender,
        EventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        PasswordEntry.IsPassword = !_isPasswordVisible;

        PasswordToggleButton.Text =
            _isPasswordVisible ? "Hide" : "Show";
    }

    private async void GoogleLogin_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        GoogleLoginCard.IsEnabled = false;

        try
        {
            await GoogleLoginCard.ScaleToAsync(
                0.96,
                90,
                Easing.CubicOut);

            await GoogleLoginCard.ScaleToAsync(
                1,
                130,
                Easing.SpringOut);

            var result = await _authService.AuthenticateExternalAsync("google")
                ?? throw new InvalidOperationException("Google did not return a session.");
            await CompleteLoginAsync(result);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Google sign-in unavailable", ex.Message, "OK");
        }
        finally
        {
            GoogleLoginCard.IsEnabled = true;
        }
    }

    private async void FacebookLogin_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        FacebookLoginCard.IsEnabled = false;

        try
        {
            await FacebookLoginCard.ScaleToAsync(
                0.96,
                90,
                Easing.CubicOut);

            await FacebookLoginCard.ScaleToAsync(
                1,
                130,
                Easing.SpringOut);

            var result = await _authService.AuthenticateExternalAsync("facebook")
                ?? throw new InvalidOperationException("Facebook did not return a session.");
            await CompleteLoginAsync(result);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Facebook sign-in unavailable", ex.Message, "OK");
        }
        finally
        {
            FacebookLoginCard.IsEnabled = true;
        }
    }

    private async void PhoneLogin_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        PhoneLoginCard.IsEnabled = false;
        try
        {
            var phoneNumber = await DisplayPromptAsync(
                "Continue with phone",
                "Enter your number in international format, for example +8801XXXXXXXXX.",
                "Send code",
                "Cancel",
                keyboard: Keyboard.Telephone);
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return;

            var challenge = await _authService.RequestPhoneCodeAsync(phoneNumber.Trim());
            var codeMessage = challenge?.DevelopmentCode is null
                ? "Enter the 6-digit code sent to your phone."
                : $"Development mode code: {challenge.DevelopmentCode}";
            var code = await DisplayPromptAsync(
                "Verify your number",
                codeMessage,
                "Verify",
                "Cancel",
                keyboard: Keyboard.Numeric,
                maxLength: 6);
            if (string.IsNullOrWhiteSpace(code))
                return;

            var result = await _authService.VerifyPhoneCodeAsync(phoneNumber.Trim(), code.Trim());
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
                throw new InvalidOperationException("The server returned an invalid response.");

            await CompleteLoginAsync(result);
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync("Connection error", "The Vibely API is not running or the API address is incorrect.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Phone sign-in failed", ex.Message, "OK");
        }
        finally
        {
            PhoneLoginCard.IsEnabled = true;
        }
    }

    private static async Task CompleteLoginAsync(AuthResponse result)
    {
        await SecureStorage.Default.SetAsync("auth_token", result.Token);
        await SecureStorage.Default.SetAsync("user_id", result.UserId.ToString());
        await SecureStorage.Default.SetAsync("user_name", result.UserName);
        await Shell.Current.GoToAsync("//MainPage");
    }

    private async void Register_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//RegisterPage");
    }

    private async void Guest_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }

    private void SetLoadingState(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
        PasswordEntry.IsEnabled = !isLoading;
        PasswordToggleButton.IsEnabled = !isLoading;
        GoogleLoginCard.IsEnabled = !isLoading;
        FacebookLoginCard.IsEnabled = !isLoading;
        PhoneLoginCard.IsEnabled = !isLoading;

        LoginButton.Text = isLoading
            ? "Signing in..."
            : "Sign in";

        LoginActivityIndicator.IsVisible = isLoading;
        LoginActivityIndicator.IsRunning = isLoading;
    }
}
