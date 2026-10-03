namespace InstagramClone.Api.Models;

public class FollowRequest
{
    public int FollowerId { get; set; }
    public User Follower { get; set; } = null!;
    public int FollowingId { get; set; }
    public User Following { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
