using InstagramClone.Mobile.Models;
using InstagramClone.Mobile.Services;

namespace InstagramClone.Mobile.Views;

public partial class EditProfilePage : ContentPage
{
    private readonly UserProfile _profile;
    private readonly SocialService _socialService;
    private string? _profileImageUrl;

    public EditProfilePage(UserProfile profile, SocialService socialService)
    {
        InitializeComponent();
        _profile = profile;
        _socialService = socialService;
        _profileImageUrl = profile.ProfileImageUrl;

        UsernameEntry.Text = profile.UserName;
        FullNameEntry.Text = profile.FullName;
        BioEditor.Text = profile.Bio;
        LinkTitleEntry.Text = profile.ProfileLinkTitle;
        LinkUrlEntry.Text = profile.ProfileLinkUrl;
        LinkEditor.IsVisible = !string.IsNullOrWhiteSpace(profile.ProfileLinkTitle) ||
                               !string.IsNullOrWhiteSpace(profile.ProfileLinkUrl);
        ProfileImage.Source = string.IsNullOrWhiteSpace(profile.ProfileImageUrl)
            ? ImageSource.FromFile("vibely_icon.png")
            : ImageSource.FromUri(new Uri(profile.ProfileImageUrl));
    }

    private void AddLinkButton_Clicked(object? sender, EventArgs e)
    {
        LinkEditor.IsVisible = true;
        LinkTitleEntry.Focus();
    }

    private void RemoveLinkButton_Clicked(object? sender, EventArgs e)
    {
        LinkTitleEntry.Text = string.Empty;
        LinkUrlEntry.Text = string.Empty;
        LinkEditor.IsVisible = false;
    }

    private async void ChangePhotoButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a profile photo",
                FileTypes = FilePickerFileType.Images
            });
            if (file is null)
                return;

            await using var stream = await file.OpenReadAsync();
            var uploaded = await _socialService.UploadMediaAsync(
                stream,
                file.FileName,
                file.ContentType ?? "image/jpeg");
            _profileImageUrl = uploaded.MediaUrl;
            ProfileImage.Source = ImageSource.FromUri(new Uri(uploaded.MediaUrl));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Photo upload failed", ex.Message, "OK");
        }
    }

    private async void SaveButton_Clicked(object? sender, EventArgs e)
    {
        var userName = UsernameEntry.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        var fullName = FullNameEntry.Text?.Trim() ?? string.Empty;
        var linkTitle = LinkTitleEntry.Text?.Trim() ?? string.Empty;
        var linkUrl = LinkUrlEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(fullName))
        {
            await DisplayAlertAsync("Missing information", "Username and name are required.", "OK");
            return;
        }

        if (LinkEditor.IsVisible && (string.IsNullOrWhiteSpace(linkTitle) || string.IsNullOrWhiteSpace(linkUrl)))
        {
            await DisplayAlertAsync("Complete your link", "Add both a title and a valid URL.", "OK");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            await _socialService.UpdateMyProfileAsync(new UpdateProfileRequest
            {
                UserName = userName,
                FullName = fullName,
                Bio = BioEditor.Text?.Trim() ?? string.Empty,
                ProfileImageUrl = _profileImageUrl,
                ProfileLinkTitle = LinkEditor.IsVisible ? linkTitle : string.Empty,
                ProfileLinkUrl = LinkEditor.IsVisible ? linkUrl : string.Empty,
                IsPrivate = _profile.IsPrivate
            });
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            SaveButton.IsEnabled = true;
            await DisplayAlertAsync("Profile update failed", ex.Message, "OK");
        }
    }

    private async void CancelButton_Clicked(object? sender, EventArgs e) =>
        await Navigation.PopModalAsync();
}
