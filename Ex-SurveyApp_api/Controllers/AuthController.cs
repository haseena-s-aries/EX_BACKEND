using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    SurveyDbContext db,
    IPasswordHasher<User> passwordHasher,
    TokenService tokenService,
    IEmailService emailService,
    ILogger<AuthController> logger) : ControllerBase
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    [HttpPost("register/send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendRegistrationOtp(
        SendRegistrationOtpRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Name is required." });
        if (request.Role is null || !Enum.IsDefined(request.Role.Value))
            return BadRequest(new { error = "Role must be Admin, Manager, Qc, Surveyor, or Client." });

        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return Conflict(new { error = "Email is already registered." });

        var registration = await db.RegistrationOtps.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var cooldownResult = GetCooldownResult(registration, now);
        if (cooldownResult is not null)
            return cooldownResult;

        registration ??= new RegistrationOtp
        {
            Email = email,
            DisplayName = name,
            RequestedRole = request.Role.Value,
            Otp = string.Empty
        };
        if (db.Entry(registration).State == EntityState.Detached)
            db.RegistrationOtps.Add(registration);

        registration.DisplayName = name;
        registration.RequestedRole = request.Role.Value;
        return await GenerateSaveAndSendOtp(registration, now, cancellationToken);
    }

    [HttpPost("register/verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyRegistrationOtp(
        VerifyRegistrationOtpRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var registration = await db.RegistrationOtps.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (registration is null)
            return NotFound(new { error = "No registration request was found for this email." });

        if (registration.ExpiresAt <= DateTimeOffset.UtcNow)
            return StatusCode(StatusCodes.Status410Gone, new { error = "OTP has expired. Request a new OTP." });

        if (!OtpMatches(registration.Otp, request.Otp))
            return BadRequest(new { error = "Invalid OTP." });

        registration.IsVerified = true;
        registration.VerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "OTP verified. You can now complete registration." });
    }

    [HttpPost("register/resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendRegistrationOtp(
        ResendRegistrationOtpRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return Conflict(new { error = "Email is already registered." });

        var registration = await db.RegistrationOtps.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (registration is null)
            return NotFound(new { error = "Start registration before requesting another OTP." });

        var now = DateTimeOffset.UtcNow;
        var cooldownResult = GetCooldownResult(registration, now);
        if (cooldownResult is not null)
            return cooldownResult;

        return await GenerateSaveAndSendOtp(registration, now, cancellationToken);
    }

    [HttpPost("register/complete")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> CompleteRegistration(
        CompleteRegistrationRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var registration = await db.RegistrationOtps.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (registration is null || !registration.IsVerified)
            return BadRequest(new { error = "Verify the email OTP before completing registration." });

        if (registration.ExpiresAt <= DateTimeOffset.UtcNow)
            return StatusCode(StatusCodes.Status410Gone,
                new { error = "The verified registration session has expired. Request and verify a new OTP." });

        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return Conflict(new { error = "Email is already registered." });

        var user = new User
        {
            Email = email,
            DisplayName = registration.DisplayName,
            PasswordHash = string.Empty,
            Role = registration.RequestedRole,
            ApprovalStatus = ApprovalStatus.Pending
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.RegistrationOtps.Remove(registration);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "Email is already registered." });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Registration completed and is pending Admin or Manager approval.",
            user = ToResponse(user)
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Invalid email or password." });

        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "Invalid email or password." });

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid email or password." });

        if (user.ApprovalStatus == ApprovalStatus.Pending)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Account is pending Admin or Manager approval.", approvalStatus = user.ApprovalStatus });
        if (user.ApprovalStatus == ApprovalStatus.Rejected)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Account registration was rejected.", approvalStatus = user.ApprovalStatus });

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
    public async Task<ActionResult<UserResponse>> CreateStaff(
        CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email, display name and password are required." });
        if (!Enum.IsDefined(request.Role))
            return BadRequest(new { error = "Invalid role." });

        var result = await CreateApprovedUser(request.Email, request.DisplayName, request.Password,
            request.Role, cancellationToken);
        if (result.Error is not null)
            return Conflict(new { error = result.Error });

        return StatusCode(StatusCodes.Status201Created, ToResponse(result.User!));
    }

    [HttpGet("approvals/pending")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<PendingApprovalResponse>>> GetPendingApprovals(
        CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(x => x.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(x => x.DisplayName)
            .Select(x => new PendingApprovalResponse(x.Id, x.Email, x.DisplayName, x.Role, x.ApprovalStatus))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPut("approvals/{userId:guid}/approve")]
    [Authorize(Roles = "Admin,Manager")]
    public Task<ActionResult<UserResponse>> ApproveUser(Guid userId, CancellationToken cancellationToken) =>
        ReviewApproval(userId, ApprovalStatus.Approved, cancellationToken);

    [HttpPut("approvals/{userId:guid}/reject")]
    [Authorize(Roles = "Admin,Manager")]
    public Task<ActionResult<UserResponse>> RejectUser(Guid userId, CancellationToken cancellationToken) =>
        ReviewApproval(userId, ApprovalStatus.Rejected, cancellationToken);

    private async Task<ActionResult<UserResponse>> ReviewApproval(
        Guid userId, ApprovalStatus decision, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            return NotFound(new { error = "User was not found." });
        if (user.ApprovalStatus != ApprovalStatus.Pending)
            return Conflict(new { error = $"User approval has already been {user.ApprovalStatus.ToString().ToLowerInvariant()}." });

        var reviewerRole = User.FindFirstValue(ClaimTypes.Role);
        if (reviewerRole == nameof(UserRole.Manager) && user.Role is UserRole.Admin or UserRole.Manager)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Only an Admin can approve or reject Admin and Manager registrations."
            });

        user.ApprovalStatus = decision;
        user.ApprovalReviewedByUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        user.ApprovalReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    private async Task<IActionResult> GenerateSaveAndSendOtp(
        RegistrationOtp registration, DateTimeOffset now, CancellationToken cancellationToken)
    {
        registration.Otp = RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");
        registration.ExpiresAt = now.Add(OtpLifetime);
        registration.LastSentAt = now;
        registration.IsVerified = false;
        registration.VerifiedAt = null;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await emailService.SendRegistrationOtpAsync(registration.Email, registration.DisplayName,
                registration.Otp, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to send registration OTP to {Email}", registration.Email);
            db.RegistrationOtps.Remove(registration);
            await db.SaveChangesAsync(CancellationToken.None);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Verification email could not be sent. Please try again later." });
        }

        return Ok(new { message = "OTP sent to the supplied email address.", expiresAt = registration.ExpiresAt });
    }

    private ObjectResult? GetCooldownResult(RegistrationOtp? registration, DateTimeOffset now)
    {
        if (registration is null)
            return null;

        var availableAt = registration.LastSentAt.Add(ResendCooldown);
        if (availableAt <= now)
            return null;

        var retryAfterSeconds = (int)Math.Ceiling((availableAt - now).TotalSeconds);
        Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        return StatusCode(StatusCodes.Status429TooManyRequests,
            new { error = "Please wait before requesting another OTP.", retryAfterSeconds });
    }

    private async Task<(User? User, string? Error)> CreateApprovedUser(
        string email, string displayName, string password, UserRole role, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await db.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
            return (null, "Email is already registered.");

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            PasswordHash = string.Empty,
            Role = role,
            ApprovalStatus = ApprovalStatus.Approved
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
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

    private static bool OtpMatches(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.ApprovalStatus);
}
