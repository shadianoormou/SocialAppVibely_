namespace InstagramClone.Mobile.Services;

public static class SessionStore
{
    public static async Task SaveAsync(string token, int userId, string userName)
    {
        await SecureStorage.Default.SetAsync("auth_token", token);
        await SecureStorage.Default.SetAsync("user_id", userId.ToString());
        await SecureStorage.Default.SetAsync("user_name", userName ?? string.Empty);
    }

    public static async Task<bool> HasTokenAsync()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(
                await SecureStorage.Default.GetAsync("auth_token"));
        }
        catch
        {
            await ClearAsync();
            return false;
        }
    }

    public static Task ClearAsync()
    {
        SecureStorage.Default.Remove("auth_token");
        SecureStorage.Default.Remove("user_id");
        SecureStorage.Default.Remove("user_name");
        return Task.CompletedTask;
    }
}

public sealed class SessionExpiredException : Exception
{
    public SessionExpiredException()
        : base("Your Vibely session has expired. Please sign in again.")
    {
    }
}
