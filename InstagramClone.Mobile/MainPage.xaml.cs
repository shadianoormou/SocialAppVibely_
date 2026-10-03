using InstagramClone.Mobile.Models;
using InstagramClone.Mobile.Services;
using InstagramClone.Mobile.Views;
using Microsoft.Maui.Controls.Shapes;

namespace InstagramClone.Mobile;

public partial class MainPage : ContentPage
{
    private readonly SocialService _socialService = new();
    private readonly List<FeedPost> _feed = [];
    private bool _isLoadingFeed;
    private bool _isLoadingProfile;
    private UserProfile? _profile;
    private IReadOnlyList<FeedPost> _profilePosts = [];
    private IReadOnlyList<FeedPost> _savedPosts = [];

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoadingFeed)
            return;

        await LoadFeedAsync();
    }

    private async Task LoadFeedAsync()
    {
        _isLoadingFeed = true;

        try
        {
            var postsTask = _socialService.GetFeedAsync();
            var storiesTask = _socialService.GetStoriesAsync();
            await Task.WhenAll(postsTask, storiesTask);
            var posts = await postsTask;
            var stories = await storiesTask;
            _feed.Clear();
            _feed.AddRange(posts);
            RenderLiveFeed(stories);
        }
        catch (HttpRequestException)
        {
            RenderEmptyState(
                "Connect to Vibely",
                "Your real feed will appear here once the API is running.");
        }
        catch (Exception)
        {
            RenderEmptyState(
                "Your feed is waiting",
                "Follow people and share your first moment to get started.");
        }
        finally
        {
            _isLoadingFeed = false;
        }
    }

    private void RenderLiveFeed(IReadOnlyList<Story> stories)
    {
        HomeView.Children.Clear();

        var content = new VerticalStackLayout { Spacing = 0 };
        content.Children.Add(BuildComposer());
        content.Children.Add(BuildSectionHeader("Stories", "See all  ›"));
        content.Children.Add(BuildStories(stories));

        if (_feed.Count == 0)
        {
            content.Children.Add(BuildEmptyState(
                "No posts yet",
                "Your feed is ready for the first real post."));
        }
        else
        {
            foreach (var post in _feed)
                content.Children.Add(BuildPostCard(post));
        }

        HomeView.Children.Add(content);
    }

    private View BuildComposer()
    {
        var plusButton = new Button
        {
            Text = "＋",
            FontSize = 27,
            TextColor = Color.FromArgb("#F0B7D0"),
            BackgroundColor = Colors.Transparent,
            Padding = 8,
            Command = new Command(async () => await CreatePostButton_Clicked())
        };

        var avatar = new Image
        {
            Source = ImageSource.FromUri(new Uri("https://i.pravatar.cc/100?img=12")),
            Aspect = Aspect.AspectFill
        };

        var avatarBorder = new Border
        {
            WidthRequest = 48,
            HeightRequest = 48,
            Stroke = Color.FromArgb("#9B7BFF"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#2D223A"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(17) },
            Content = avatar
        };

        var copy = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "Share a little magic", TextColor = Colors.White, FontFamily = "OpenSansSemibold", FontSize = 14 },
                new Label { Text = "What is inspiring you today?", TextColor = Color.FromArgb("#A8A3B3"), FontSize = 12 }
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        grid.Add(avatarBorder);
        grid.Add(copy, 1);
        grid.Add(plusButton, 2);

        return new Border
        {
            Margin = new Thickness(18, 3, 18, 18),
            Padding = new Thickness(14, 16),
            BackgroundColor = Color.FromArgb("#17151F"),
            Stroke = Color.FromArgb("#352A48"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) },
            Content = grid
        };
    }

    private View BuildStories(IReadOnlyList<Story> liveStories)
    {
        var storyStrip = new HorizontalStackLayout
        {
            Padding = new Thickness(18, 0, 18, 22),
            Spacing = 16
        };

        var ownStory = new VerticalStackLayout
        {
            WidthRequest = 64,
            Spacing = 7,
            Children =
            {
                new Border
                {
                    WidthRequest = 64,
                    HeightRequest = 64,
                    Padding = 3,
                    Stroke = Color.FromArgb("#9B7BFF"),
                    StrokeThickness = 2,
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
                    Content = new Image
                    {
                        Source = ImageSource.FromUri(new Uri("https://i.pravatar.cc/100?img=12")),
                        Aspect = Aspect.AspectFill
                    }
                },
                new Label
                {
                    Text = "Your story",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#A8A3B3"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
        ownStory.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await CreatePostButton_Clicked())
        });
        storyStrip.Children.Add(ownStory);

        foreach (var story in liveStories)
        {
            var storyLayout = new VerticalStackLayout
            {
                WidthRequest = 64,
                Spacing = 7,
                Children =
                {
                    new Border
                    {
                        WidthRequest = 64,
                        HeightRequest = 64,
                        Padding = 3,
                        Stroke = Color.FromArgb("#F0B7D0"),
                        StrokeThickness = 2,
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
                        Content = new Image
                        {
                            Source = ImageSource.FromUri(new Uri(GetMediaUri(story.ProfileImageUrl))),
                            Aspect = Aspect.AspectFill
                        }
                    },
                    new Label
                    {
                        Text = story.UserName,
                        FontSize = 10,
                        TextColor = Colors.White,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };
            storyLayout.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                    await DisplayAlertAsync(story.UserName, story.Text ?? "Story opened.", "Close"))
            });
            storyStrip.Children.Add(storyLayout);
        }

        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = storyStrip
        };
    }

    private View BuildSectionHeader(string title, string action)
    {
        var grid = new Grid
        {
            Padding = new Thickness(20, 0, 18, 12),
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star), new(GridLength.Auto)
            }
        };
        grid.Add(new Label
        {
            Text = title,
            FontFamily = "OpenSansSemibold",
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            TextColor = Colors.White
        });
        grid.Add(new Label
        {
            Text = action,
            FontSize = 12,
            TextColor = Color.FromArgb("#C4B5FD")
        }, 1);
        return grid;
    }

    private View BuildPostCard(FeedPost post)
    {
        var profileImage = new Image
        {
            Source = ImageSource.FromUri(new Uri(GetMediaUri(post.ProfileImageUrl))),
            Aspect = Aspect.AspectFill
        };

        var avatar = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            Stroke = Color.FromArgb("#C4B5FD"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(15) },
            Content = profileImage
        };

        var metadata = new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                new Label { Text = post.UserName, FontFamily = "OpenSansSemibold", FontAttributes = FontAttributes.Bold, FontSize = 13, TextColor = Colors.White },
                new Label { Text = BuildMeta(post), FontSize = 11, TextColor = Color.FromArgb("#8D879D") }
            }
        };

        var moreButton = new Button
        {
            Text = "•••",
            FontSize = 17,
            TextColor = Color.FromArgb("#A8A3B3"),
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(8, 0)
        };
        moreButton.Clicked += async (_, _) =>
            await DisplayActionSheetAsync("Post options", "Cancel", null, "Not interested", "Report", "Copy link");

        var header = new Grid
        {
            Padding = new Thickness(15, 14),
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        header.Add(avatar);
        header.Add(metadata, 1);
        header.Add(moreButton, 2);

        var media = new Image
        {
            Source = ImageSource.FromUri(new Uri(GetMediaUri(post.MediaUrl))),
            HeightRequest = 350,
            Aspect = Aspect.AspectFill
        };

        var likeButton = new Button
        {
            Text = post.LikedByMe ? "♥" : "♡",
            FontSize = 28,
            TextColor = post.LikedByMe ? Color.FromArgb("#F0B7D0") : Colors.White,
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };
        var saveButton = new Button
        {
            Text = post.SavedByMe ? "♣" : "♧",
            FontSize = 27,
            TextColor = post.SavedByMe ? Color.FromArgb("#C4B5FD") : Colors.White,
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };
        var likesLabel = new Label
        {
            Text = $"Liked by {post.LikesCount:N0} people",
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };
        var commentsLabel = new Label
        {
            Text = $"View all {post.CommentsCount:N0} comments",
            TextColor = Color.FromArgb("#8D879D"),
            FontSize = 12
        };

        likeButton.Clicked += async (_, _) =>
        {
            try
            {
                var result = await _socialService.ToggleLikeAsync(post.Id);
                post.LikedByMe = result.Liked;
                post.LikesCount = result.LikesCount;
                likeButton.Text = result.Liked ? "♥" : "♡";
                likeButton.TextColor = result.Liked ? Color.FromArgb("#F0B7D0") : Colors.White;
                likesLabel.Text = $"Liked by {result.LikesCount:N0} people";
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Like failed", ex.Message, "OK");
            }
        };

        saveButton.Clicked += async (_, _) =>
        {
            try
            {
                var saved = await _socialService.ToggleSaveAsync(post.Id);
                post.SavedByMe = saved;
                saveButton.Text = saved ? "♣" : "♧";
                saveButton.TextColor = saved ? Color.FromArgb("#C4B5FD") : Colors.White;
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Save failed", ex.Message, "OK");
            }
        };

        var commentButton = new Button
        {
            Text = "◌",
            FontSize = 27,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };
        commentButton.Clicked += async (_, _) =>
        {
            var text = await DisplayPromptAsync("Add comment", "Say something kind", "Post", "Cancel");
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                await _socialService.AddCommentAsync(post.Id, text.Trim());
                post.CommentsCount++;
                commentsLabel.Text = $"View all {post.CommentsCount:N0} comments";
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Comment failed", ex.Message, "OK");
            }
        };

        var shareButton = new Button
        {
            Text = "⌁",
            FontSize = 27,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };
        shareButton.Clicked += async (_, _) =>
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Uri = post.MediaUrl,
                Title = $"Vibely post by @{post.UserName}"
            });

        var actions = new Grid
        {
            Padding = new Thickness(15, 12, 15, 2),
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto),
                new(GridLength.Star), new(GridLength.Auto)
            },
            ColumnSpacing = 13
        };
        actions.Add(likeButton);
        actions.Add(commentButton, 1);
        actions.Add(shareButton, 2);
        actions.Add(new BoxView(), 3);
        actions.Add(saveButton, 4);

        var caption = new VerticalStackLayout
        {
            Padding = new Thickness(15, 0, 15, 17),
            Spacing = 5,
            Children =
            {
                likesLabel,
                new Label
                {
                    Text = $"{post.UserName}  {post.Caption}",
                    TextColor = Color.FromArgb("#E9E6EF"),
                    FontSize = 13
                },
                commentsLabel
            }
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { header, media, actions, caption }
        };

        return new Border
        {
            Margin = new Thickness(12, 0, 12, 22),
            BackgroundColor = Color.FromArgb("#15151D"),
            Stroke = Color.FromArgb("#302B3E"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
            Content = stack
        };
    }

    private void RenderEmptyState(string title, string description)
    {
        HomeView.Children.Clear();
        var content = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                BuildComposer(),
                BuildSectionHeader("Stories", "See all  ›"),
                BuildStories([]),
                BuildEmptyState(title, description)
            }
        };
        HomeView.Children.Add(content);
    }

    private static View BuildEmptyState(string title, string description) =>
        new Border
        {
            Margin = new Thickness(18, 24),
            Padding = new Thickness(24),
            BackgroundColor = Color.FromArgb("#17151F"),
            Stroke = Color.FromArgb("#352A48"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) },
            Content = new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    new Label { Text = "✦", TextColor = Color.FromArgb("#D6B36A"), FontSize = 26, HorizontalOptions = LayoutOptions.Center },
                    new Label { Text = title, TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 17, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = description, TextColor = Color.FromArgb("#A8A3B3"), FontSize = 13, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };

    private string BuildMeta(FeedPost post)
    {
        var age = DateTime.UtcNow - post.CreatedAtUtc.ToUniversalTime();
        var relative = age.TotalMinutes < 60
            ? $"{Math.Max(1, (int)age.TotalMinutes)} min ago"
            : age.TotalHours < 24
                ? $"{(int)age.TotalHours} hr ago"
                : $"{(int)age.TotalDays} d ago";

        return string.IsNullOrWhiteSpace(post.Location)
            ? relative
            : $"{post.Location} · {relative}";
    }

    private static string GetMediaUri(string? mediaUrl) =>
        string.IsNullOrWhiteSpace(mediaUrl)
            ? "https://i.pravatar.cc/100?img=12"
            : mediaUrl;

    private void ShowView(View view, string context)
    {
        HomeView.IsVisible = view == HomeView;
        ExploreView.IsVisible = view == ExploreView;
        ActivityView.IsVisible = view == ActivityView;
        ProfileView.IsVisible = view == ProfileView;
        ContextLabel.Text = context;
        MainScroll.ScrollToAsync(0, 0, false);
    }

    private void HomeNavButton_Clicked(object? sender, EventArgs e) =>
        ShowView(HomeView, "Your visual world");

    private void ExploreNavButton_Clicked(object? sender, EventArgs e) =>
        ShowView(ExploreView, "Curated for you");

    private void ActivityNavButton_Clicked(object? sender, EventArgs e) =>
        ShowView(ActivityView, "Notifications & connections");

    private async void ProfileNavButton_Clicked(object? sender, EventArgs e)
    {
        ShowView(ProfileView, "Your profile");
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        if (_isLoadingProfile)
            return;

        _isLoadingProfile = true;
        ProfileView.Children.Clear();
        ProfileView.Children.Add(BuildEmptyState("Loading your profile", "Syncing your real Vibely presence."));

        try
        {
            var profileTask = _socialService.GetMyProfileAsync();
            var postsTask = _socialService.GetMyPostsAsync("posts");
            var savedTask = _socialService.GetMyPostsAsync("saved");
            await Task.WhenAll(profileTask, postsTask, savedTask);

            _profile = await profileTask;
            _profilePosts = await postsTask;
            _savedPosts = await savedTask;

            if (_profile is null)
                throw new InvalidOperationException("Profile data was empty.");

            RenderProfile(_profile, _profilePosts, _savedPosts);
        }
        catch (HttpRequestException)
        {
            RenderProfileUnavailable("Connect to Vibely", "Sign in and start the API to manage your real profile.");
        }
        catch (Exception)
        {
            RenderProfileUnavailable("Your profile is private", "Sign in to view stats, saved posts, and profile controls.");
        }
        finally
        {
            _isLoadingProfile = false;
        }
    }

    private void RenderProfile(
        UserProfile profile,
        IReadOnlyList<FeedPost> posts,
        IReadOnlyList<FeedPost> saved)
    {
        ProfileView.Children.Clear();

        var avatar = new Image
        {
            Source = ImageSource.FromUri(new Uri(GetMediaUri(profile.ProfileImageUrl))),
            Aspect = Aspect.AspectFill
        };
        var avatarBorder = new Border
        {
            WidthRequest = 92,
            HeightRequest = 92,
            Padding = 3,
            Stroke = Color.FromArgb("#D6B36A"),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(32) },
            Content = avatar
        };

        var stats = new HorizontalStackLayout
        {
            Spacing = 18,
            Children =
            {
                BuildProfileStat(profile.PostsCount, "Posts"),
                BuildProfileStat(profile.FollowersCount, "Followers"),
                BuildProfileStat(profile.FollowingCount, "Following")
            }
        };
        var identity = new VerticalStackLayout
        {
            Spacing = 7,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = $"@{profile.UserName}", TextColor = Colors.White, FontFamily = "OpenSansSemibold", FontAttributes = FontAttributes.Bold, FontSize = 17 },
                stats
            }
        };
        var settings = new Button
        {
            Text = "⚙",
            FontSize = 20,
            TextColor = Color.FromArgb("#A8A3B3"),
            BackgroundColor = Colors.Transparent,
            Padding = 2,
            Command = new Command(async () => await ShowSettingsAsync())
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)
            },
            ColumnSpacing = 14
        };
        header.Add(avatarBorder);
        header.Add(identity, 1);
        header.Add(settings, 2);

        var displayName = string.IsNullOrWhiteSpace(profile.FullName)
            ? profile.UserName
            : profile.FullName;
        var accountLabel = profile.IsPrivate ? "Private account  ·  visual storyteller" : "Creator account  ·  visual storyteller";
        var bio = string.IsNullOrWhiteSpace(profile.Bio)
            ? "Curating quiet moments, bold spaces & good energy."
            : profile.Bio;
        var bioBlock = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = displayName, TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 16 },
                new Label { Text = accountLabel, TextColor = Color.FromArgb("#D6B36A"), FontSize = 12 },
                new Label { Text = bio, TextColor = Color.FromArgb("#C9C3D4"), FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }
            }
        };
        if (!string.IsNullOrWhiteSpace(profile.ProfileLinkUrl))
        {
            var profileLink = new Button
            {
                Text = $"↗  {(!string.IsNullOrWhiteSpace(profile.ProfileLinkTitle) ? profile.ProfileLinkTitle : profile.ProfileLinkUrl)}",
                TextColor = Color.FromArgb("#C4B5FD"),
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(0, 3),
                HorizontalOptions = LayoutOptions.Start,
                FontSize = 12
            };
            profileLink.Clicked += async (_, _) =>
            {
                try
                {
                    await Launcher.Default.OpenAsync(profile.ProfileLinkUrl);
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Link unavailable", ex.Message, "OK");
                }
            };
            bioBlock.Children.Add(profileLink);
        }

        var editButton = new Button
        {
            Text = "Edit profile",
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#1C1B25"),
            BorderColor = Color.FromArgb("#514366"),
            BorderWidth = 1,
            CornerRadius = 14,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };
        editButton.Clicked += async (_, _) => await EditProfileAsync();
        var shareButton = new Button
        {
            Text = "Share profile",
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#1C1B25"),
            BorderColor = Color.FromArgb("#514366"),
            BorderWidth = 1,
            CornerRadius = 14,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };
        shareButton.Clicked += async (_, _) => await ShareProfileAsync();
        var profileActions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star), new(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        profileActions.Add(editButton);
        profileActions.Add(shareButton, 1);

        var highlights = new HorizontalStackLayout
        {
            Spacing = 12,
            Children =
            {
                BuildHighlight("✦", "New"),
                BuildHighlight("◌", "Mood"),
                BuildHighlight("♧", "Saved")
            }
        };

        var postsGrid = new Grid { ColumnSpacing = 5, RowSpacing = 5 };
        var postsTab = BuildProfileTab("Posts", true);
        var savedTab = BuildProfileTab("Saved", false);
        var taggedTab = BuildProfileTab("Tagged", false);
        var tabs = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)
            },
            Margin = new Thickness(0, 3, 0, 0)
        };
        tabs.Add(postsTab, 0);
        tabs.Add(savedTab, 1);
        tabs.Add(taggedTab, 2);

        void SetTab(string tab)
        {
            SetProfileTabState(postsTab, tab == "posts");
            SetProfileTabState(savedTab, tab == "saved");
            SetProfileTabState(taggedTab, tab == "tagged");
            postsGrid.Children.Clear();
            var selected = tab == "posts" ? posts : tab == "saved" ? saved : [];
            if (tab == "tagged")
            {
                postsGrid.Children.Add(BuildEmptyState("No tagged posts", "Posts you are tagged in will appear here."));
                return;
            }

            AddProfilePostGrid(postsGrid, selected);
        }

        postsTab.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetTab("posts")) });
        savedTab.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetTab("saved")) });
        taggedTab.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetTab("tagged")) });
        SetTab("posts");

        var content = new VerticalStackLayout
        {
            Spacing = 17,
            Children =
            {
                header,
                bioBlock,
                profileActions,
                highlights,
                new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#272532") },
                tabs,
                postsGrid,
                new Button { Text = "Sign out", TextColor = Color.FromArgb("#F0B7D0"), BackgroundColor = Colors.Transparent, FontAttributes = FontAttributes.Bold, Command = new Command(async () => await SignOutAsync()) }
            }
        };
        ProfileView.Children.Add(content);
    }

    private static View BuildProfileStat(int value, string label) =>
        new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                new Label { Text = value.ToString("N0"), TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 16 },
                new Label { Text = label, TextColor = Color.FromArgb("#A8A3B3"), FontSize = 10 }
            }
        };

    private static Border BuildHighlight(string icon, string label) =>
        new Border
        {
            WidthRequest = 66,
            Padding = new Thickness(7, 8),
            BackgroundColor = Color.FromArgb("#1C1B25"),
            Stroke = Color.FromArgb("#3A3448"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(18) },
            Content = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = icon, TextColor = Color.FromArgb("#D6B36A"), FontSize = 18, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = label, TextColor = Color.FromArgb("#C9C3D4"), FontSize = 10, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };

    private static VerticalStackLayout BuildProfileTab(string text, bool selected) =>
        new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                new Label { Text = text, TextColor = selected ? Color.FromArgb("#F0B7D0") : Color.FromArgb("#777181"), FontAttributes = FontAttributes.Bold, FontSize = 11, HorizontalTextAlignment = TextAlignment.Center },
                new BoxView { HeightRequest = 2, BackgroundColor = selected ? Color.FromArgb("#D6B36A") : Colors.Transparent, CornerRadius = 1 }
            }
        };

    private static void SetProfileTabState(VerticalStackLayout tab, bool selected)
    {
        if (tab.Children[0] is Label label)
            label.TextColor = selected ? Color.FromArgb("#F0B7D0") : Color.FromArgb("#777181");
        if (tab.Children[1] is BoxView underline)
            underline.BackgroundColor = selected ? Color.FromArgb("#D6B36A") : Colors.Transparent;
    }

    private void AddProfilePostGrid(Grid grid, IReadOnlyList<FeedPost> posts)
    {
        if (posts.Count == 0)
        {
            grid.Children.Add(BuildEmptyState("No posts yet", "Your published moments will live here."));
            return;
        }

        var rows = (int)Math.Ceiling(posts.Count / 3d);
        for (var row = 0; row < rows; row++)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112) });

        for (var index = 0; index < posts.Count; index++)
        {
            var post = posts[index];
            var cell = new Border
            {
                BackgroundColor = Color.FromArgb("#1C1B25"),
                StrokeThickness = 0,
                Content = new Image
                {
                    Source = ImageSource.FromUri(new Uri(GetMediaUri(post.MediaUrl))),
                    Aspect = Aspect.AspectFill
                }
            };
            cell.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await DisplayAlertAsync("Post", post.Caption, "Close"))
            });
            grid.Add(cell, index % 3, index / 3);
        }
    }

    private async Task EditProfileAsync()
    {
        if (_profile is null)
            return;

        await Navigation.PushModalAsync(new EditProfilePage(_profile, _socialService));
        await LoadProfileAsync();
    }

    private async Task ShareProfileAsync()
    {
        if (_profile is null)
            return;

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"Vibely profile · @{_profile.UserName}",
            Text = $"Follow @{_profile.UserName} on Vibely. {_profile.Bio}"
        });
    }

    private void RenderProfileUnavailable(string title, string description)
    {
        ProfileView.Children.Clear();
        ProfileView.Children.Add(BuildEmptyState(title, description));
    }

    private void SearchButton_Clicked(object? sender, EventArgs e)
    {
        SearchPanel.IsVisible = true;
        SearchEntry.Focus();
    }

    private void CloseSearchButton_Clicked(object? sender, EventArgs e)
    {
        SearchEntry.Text = string.Empty;
        SearchPanel.IsVisible = false;
    }

    private async void MoreButton_Clicked(object? sender, EventArgs e) =>
        await DisplayActionSheetAsync("Post options", "Cancel", null, "Not interested", "Report", "Copy link");

    private void LikeButton1_Clicked(object? sender, EventArgs e)
    {
        LikeButton1.Text = LikeButton1.Text == "♥" ? "♡" : "♥";
        LikeButton1.TextColor = LikeButton1.Text == "♥" ? Color.FromArgb("#F0B7D0") : Colors.White;
    }

    private void LikeButton2_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        button.Text = button.Text == "♥" ? "♡" : "♥";
        button.TextColor = button.Text == "♥" ? Color.FromArgb("#F0B7D0") : Colors.White;
    }

    private async void CommentButton_Clicked(object? sender, EventArgs e) =>
        await DisplayPromptAsync("Add comment", "Say something kind", "Post", "Cancel");

    private async void ShareButton_Clicked(object? sender, EventArgs e) =>
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Vibely",
            Text = "Discover this moment on Vibely."
        });

    private void SaveButton1_Clicked(object? sender, EventArgs e)
    {
        SaveButton1.Text = SaveButton1.Text == "♣" ? "♧" : "♣";
        SaveButton1.TextColor = SaveButton1.Text == "♣" ? Color.FromArgb("#C4B5FD") : Colors.White;
    }

    private async void MessagesButton_Clicked(object? sender, EventArgs e) =>
        await DisplayAlertAsync("Messages", "Messaging is ready for the conversation API.", "Done");

    private async void CreatePostButton_Clicked(object? sender, EventArgs e) =>
        await CreatePostButton_Clicked();

    private async Task CreatePostButton_Clicked()
    {
        var action = await DisplayActionSheetAsync(
            "Create on Vibely",
            "Cancel",
            null,
            "New post",
            "Story",
            "Reel");

        if (action != "New post")
        {
            if (action is "Story" or "Reel")
                await DisplayAlertAsync(action, "This composer is connected to the same media pipeline and is ready for its endpoint.", "Continue");
            return;
        }

        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a photo for your post",
                FileTypes = FilePickerFileType.Images
            });

            if (file is null)
                return;

            var caption = await DisplayPromptAsync("Caption", "Tell your story", "Publish", "Cancel");
            if (caption is null)
                return;

            await using var stream = await file.OpenReadAsync();
            var uploaded = await _socialService.UploadMediaAsync(
                stream,
                file.FileName,
                file.ContentType ?? "image/jpeg");

            await _socialService.CreatePostAsync(new CreatePostRequest
            {
                Caption = caption.Trim(),
                MediaUrl = uploaded.MediaUrl,
                MediaType = uploaded.MediaType
            });

            await LoadFeedAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Post could not be published", ex.Message, "OK");
        }
    }

    private async void SettingsButton_Clicked(object? sender, EventArgs e) =>
        await ShowSettingsAsync();

    private async Task ShowSettingsAsync() =>
        await DisplayActionSheetAsync("Settings", "Close", null, "Edit account", "Privacy", "Notifications");

    private async void SignOutButton_Clicked(object? sender, EventArgs e)
        => await SignOutAsync();

    private async Task SignOutAsync()
    {
        var confirm = await DisplayAlertAsync("Sign out?", "You can always come back to your visual world.", "Sign out", "Stay");
        if (confirm)
        {
            SecureStorage.Default.Remove("auth_token");
            SecureStorage.Default.Remove("user_id");
            SecureStorage.Default.Remove("user_name");
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
