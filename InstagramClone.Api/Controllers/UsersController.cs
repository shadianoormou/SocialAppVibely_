using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public UsersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMyProfile()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserProfileResponse
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                FullName = x.FullName,
                Bio = x.Bio,
                ProfileImageUrl = x.ProfileImageUrl,
                IsPrivate = x.IsPrivate,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound(new
            {
                message = "User was not found."
            });
        }

        return Ok(user);
    }
}