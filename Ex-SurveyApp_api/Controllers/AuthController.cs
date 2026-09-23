using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    SurveyDbContext db,
    IPasswordHasher<User> passwordHasher,
    TokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email, display name and password are required." });

        var result = await CreateUser(request.Email, request.DisplayName, request.Password,
            UserRole.Client, cancellationToken);
        if (result.Error is not null)
            return Conflict(new { error = result.Error });

        return Ok(CreateAuthResponse(result.User!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Invalid email or password." });

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "Invalid email or password." });

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid email or password." });

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(CreateAuthResponse(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.AsNoTracking().FirstAsync(x => x.Id == id, cancellationToken);
        return Ok(ToResponse(user));
    }

    [HttpPost("users")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<UserResponse>> CreateStaff(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email, display name and password are required." });

        if (!Enum.IsDefined(request.Role))
            return BadRequest(new { error = "Invalid role." });

        var result = await CreateUser(request.Email, request.DisplayName, request.Password,
            request.Role, cancellationToken);
        if (result.Error is not null)
            return Conflict(new { error = result.Error });

        return StatusCode(StatusCodes.Status201Created, ToResponse(result.User!));
    }

    private async Task<(User? User, string? Error)> CreateUser(
        string email, string displayName, string password, UserRole role, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
            return (null, "Email is already registered.");

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            PasswordHash = string.Empty,
            Role = role
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return (null, "Email is already registered.");
        }
        return (user, null);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var (token, expiresAt) = tokenService.Create(user);
        return new AuthResponse(token, expiresAt, ToResponse(user));
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role);
}

public record RegisterRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(200)] string DisplayName,
    [Required, StringLength(128, MinimumLength = 12)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record CreateUserRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(200)] string DisplayName,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required] UserRole Role);

public record UserResponse(Guid Id, string Email, string DisplayName, UserRole Role);
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
