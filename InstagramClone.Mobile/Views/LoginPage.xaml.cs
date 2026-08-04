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

            await SecureStorage.Default.SetAsync(
                "auth_token",
                result.Token);

            await SecureStorage.Default.SetAsync(
                "user_id",
                result.UserId.ToString());

            await SecureStorage.Default.SetAsync(
                "user_name",
                result.UserName);

            await LoginButton.ScaleToAsync(
                0.96,
                90,
                Easing.CubicOut);

            await LoginButton.ScaleToAsync(
                1,
                130,
                Easing.SpringOut);

            await DisplayAlertAsync(
                "Welcome to Vibely",
                $"Signed in successfully as @{result.UserName}.",
                "Continue");
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

            await DisplayAlertAsync(
                "Google Sign-In",
                "Google OAuth configuration will be added next.",
                "OK");
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

            await DisplayAlertAsync(
                "Facebook Sign-In",
                "Facebook OAuth configuration will be added next.",
                "OK");
        }
        finally
        {
            FacebookLoginCard.IsEnabled = true;
        }
    }

    private async void Register_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await DisplayAlertAsync(
            "Create account",
            "The premium registration page will open here.",
            "OK");
    }

    private void SetLoadingState(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
        PasswordEntry.IsEnabled = !isLoading;
        PasswordToggleButton.IsEnabled = !isLoading;
        GoogleLoginCard.IsEnabled = !isLoading;
        FacebookLoginCard.IsEnabled = !isLoading;

        LoginButton.Text = isLoading
            ? "Signing in..."
            : "Sign in";

        LoginActivityIndicator.IsVisible = isLoading;
        LoginActivityIndicator.IsRunning = isLoading;
    }
}
