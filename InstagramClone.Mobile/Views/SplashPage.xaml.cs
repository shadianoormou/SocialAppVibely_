namespace InstagramClone.Mobile.Views;

public partial class SplashPage : ContentPage
{
    private bool _hasStarted;

    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasStarted)
            return;

        _hasStarted = true;

        try
        {
            PrepareAnimation();
            await PlayEntranceAsync();
            await Task.Delay(900);

            var destination = await GetDestinationAsync();
            await SplashRoot.FadeToAsync(0, 260, Easing.CubicIn);
            await Shell.Current.GoToAsync(destination, false);
        }
        catch
        {
            await Shell.Current.GoToAsync("//LoginPage", false);
        }
    }

    private void PrepareAnimation()
    {
        BrandArtwork.Opacity = 0;
        BrandArtwork.Scale = 0.82;
        BrandArtwork.TranslationY = 16;

        Tagline.Opacity = 0;
        Tagline.TranslationY = 12;

        AccentLine.Opacity = 0;
        AccentLine.ScaleX = 0.12;

        LoadingLabel.Opacity = 0;

        OuterHalo.Opacity = 0;
        OuterHalo.Scale = 0.72;
        InnerHalo.Opacity = 0;
        InnerHalo.Scale = 0.76;
    }

    private async Task PlayEntranceAsync()
    {
        await Task.WhenAll(
            OuterHalo.FadeToAsync(0.8, 700, Easing.CubicOut),
            OuterHalo.ScaleToAsync(1, 1100, Easing.CubicOut),
            InnerHalo.FadeToAsync(0.9, 750, Easing.CubicOut),
            InnerHalo.ScaleToAsync(1, 1050, Easing.CubicOut),
            BrandArtwork.FadeToAsync(1, 650, Easing.CubicOut),
            BrandArtwork.ScaleToAsync(1, 950, Easing.SpringOut),
            BrandArtwork.TranslateToAsync(0, 0, 850, Easing.CubicOut));

        await Task.WhenAll(
            Tagline.FadeToAsync(1, 520, Easing.CubicOut),
            Tagline.TranslateToAsync(0, 0, 620, Easing.CubicOut),
            AccentLine.FadeToAsync(1, 450, Easing.CubicOut),
            AccentLine.ScaleXToAsync(1, 700, Easing.CubicInOut),
            LoadingLabel.FadeToAsync(1, 650, Easing.CubicOut));

        await Task.WhenAll(
            OuterHalo.ScaleToAsync(1.05, 850, Easing.SinInOut),
            InnerHalo.ScaleToAsync(0.96, 850, Easing.SinInOut),
            BrandArtwork.ScaleToAsync(1.015, 850, Easing.SinInOut));
    }

    private static async Task<string> GetDestinationAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync("auth_token");
            return string.IsNullOrWhiteSpace(token)
                ? "//LoginPage"
                : "//MainPage";
        }
        catch
        {
            SecureStorage.Default.Remove("auth_token");
            return "//LoginPage";
        }
    }
}
