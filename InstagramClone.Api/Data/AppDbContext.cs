using InstagramClone.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostSave> PostSaves => Set<PostSave>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<PhoneOtpChallenge> PhoneOtpChallenges => Set<PhoneOtpChallenge>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.UserName)
                .IsUnique();

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.HasIndex(x => x.PhoneNumber)
                .IsUnique();
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AuthorId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<PostLike>(entity =>
        {
            entity.HasKey(x => new { x.PostId, x.UserId });
            entity.HasOne(x => x.Post).WithMany(x => x.Likes).HasForeignKey(x => x.PostId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostSave>(entity =>
        {
            entity.HasKey(x => new { x.PostId, x.UserId });
            entity.HasOne(x => x.Post).WithMany(x => x.Saves).HasForeignKey(x => x.PostId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostComment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PostId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasKey(x => new { x.FollowerId, x.FollowingId });
            entity.HasOne(x => x.Follower).WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Following).WithMany().HasForeignKey(x => x.FollowingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AuthorId, x.ExpiresAtUtc });
        });

        modelBuilder.Entity<PhoneOtpChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PhoneNumber, x.CreatedAtUtc });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(40);
            entity.HasOne(x => x.Recipient)
                .WithMany()
                .HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.RecipientId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<DirectMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Recipient)
                .WithMany()
                .HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SenderId, x.RecipientId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.RecipientId, x.ReadAtUtc });
        });
    }
}
