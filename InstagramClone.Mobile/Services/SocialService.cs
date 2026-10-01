using System.Net.Http.Headers;
using System.Net.Http.Json;
using InstagramClone.Mobile.Models;

namespace InstagramClone.Mobile.Services;

public class SocialService
{
    private readonly HttpClient _httpClient;

    public SocialService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(GetBaseAddress()),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IReadOnlyList<FeedPost>> GetFeedAsync()
    {
        await AddTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<FeedPost>>("api/feed?take=30")
            ?? [];
    }

    public async Task<IReadOnlyList<Story>> GetStoriesAsync()
    {
        await AddTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<Story>>("api/stories")
            ?? [];
    }

    public async Task<UserProfile?> GetMyProfileAsync()
    {
        await AddTokenAsync();
        using var response = await _httpClient.GetAsync("api/users/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>();
    }

    public async Task<UserProfile?> UpdateMyProfileAsync(UpdateProfileRequest request)
    {
        await AddTokenAsync();
        using var response = await _httpClient.PutAsJsonAsync("api/users/me", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>();
    }

    public async Task<IReadOnlyList<FeedPost>> GetMyPostsAsync(string tab = "posts")
    {
        await AddTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<FeedPost>>(
            $"api/users/me/posts?tab={Uri.EscapeDataString(tab)}") ?? [];
    }

    public async Task<(bool Liked, int LikesCount)> ToggleLikeAsync(int postId)
    {
        await AddTokenAsync();
        using var response = await _httpClient.PostAsync($"api/posts/{postId}/like", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LikeResult>();
        return (result?.Liked ?? false, result?.LikesCount ?? 0);
    }

    public async Task<bool> ToggleSaveAsync(int postId)
    {
        await AddTokenAsync();
        using var response = await _httpClient.PostAsync($"api/posts/{postId}/save", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SaveResult>();
        return result?.Saved ?? false;
    }

    public async Task<Comment?> AddCommentAsync(int postId, string text)
    {
        await AddTokenAsync();
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/posts/{postId}/comments",
            new { Text = text });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Comment>();
    }

    public async Task<FeedPost?> CreatePostAsync(CreatePostRequest request)
    {
        await AddTokenAsync();
        using var response = await _httpClient.PostAsJsonAsync("api/posts", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FeedPost>();
    }

    public async Task<(string MediaUrl, string MediaType)> UploadMediaAsync(
        Stream stream,
        string fileName,
        string contentType)
    {
        await AddTokenAsync();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var response = await _httpClient.PostAsync("api/media/upload", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MediaResult>()
            ?? throw new InvalidOperationException("The media service returned an empty response.");
        return (result.MediaUrl, result.MediaType);
    }

    private async Task AddTokenAsync()
    {
        var token = await SecureStorage.Default.GetAsync("auth_token");
        _httpClient.DefaultRequestHeaders.Authorization =
            string.IsNullOrWhiteSpace(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);
    }

    private static string GetBaseAddress()
    {
#if ANDROID
        return "http://10.0.2.2:5112/";
#else
        return "http://127.0.0.1:5112/";
#endif
    }

    private sealed class LikeResult
    {
        public bool Liked { get; set; }
        public int LikesCount { get; set; }
    }

    private sealed class SaveResult
    {
        public bool Saved { get; set; }
    }

    private sealed class MediaResult
    {
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = "image";
    }
}
