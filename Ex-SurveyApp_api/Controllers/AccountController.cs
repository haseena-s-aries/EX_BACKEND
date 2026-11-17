using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Ex_SurveyApp_api.Controllers;

[ApiController, Authorize, Route("api/account")]
[EndpointScopeInventory(EndpointScopeKind.IdentityLifecycle)]
public sealed class AccountController(SurveyDbContext db, IPasswordHasher<User> hasher, IEmailService email, IAccountMailQueue mailQueue,
    IProjectPhotoStorage storage, IConfiguration config, IWebHostEnvironment environment, ILogger<AccountController> logger) : ControllerBase
{
    public const string GenericResetMessage = "If an account exists with this email, a password reset link has been sent.";
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static bool Eligible(User? user) => user is {IsActive: true, ApprovalStatus: ApprovalStatus.Approved};
    private static bool HasLocalPassword(User user) => !string.IsNullOrWhiteSpace(user.PasswordHash);
    private static string Normalize(string input) => input.Trim().ToLowerInvariant();
    public static string? PhotoUrl(User user) => user.ProfilePhotoPath is null ? null : "/api/account/profile/photo?v=" + Uri.EscapeDataString(Path.GetFileName(user.ProfilePhotoPath));
    private async Task<User?> Current(CancellationToken ct) => await db.Users.SingleOrDefaultAsync(u => u.Id == Actor, ct);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private bool PasswordMatches(User user, string password) => hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    // Registration currently requires a nonempty password, with no sixteen-character minimum.
    private static bool ValidPassword(string password) => !string.IsNullOrWhiteSpace(password) && password.Length <= 256;

    private async Task<object> Describe(User user, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var pending = await db.AccountActionTokens.AsNoTracking().Where(t => t.UserId == user.Id && t.Purpose == "EmailChange" && t.ConsumedAt == null && t.ExpiresAt > now && t.AuthVersion == user.AuthVersion)
            .OrderByDescending(t => t.CreatedAt).Select(t => t.NewEmail).FirstOrDefaultAsync(ct);
        return new {user.Id, user.Email, user.DisplayName, user.PhoneNumber, user.Designation, user.Role,
            user.ApprovalStatus, user.IsActive, ProfilePhotoUrl = PhotoUrl(user), PendingEmail = pending, CanChangePassword = !string.IsNullOrWhiteSpace(user.PasswordHash)};
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await Describe(user!, ct));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(ProfileWrite request, CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return BadRequest(new {error = "Full Name is required."});
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber.Count(char.IsDigit) is < 7 or > 15)
            return BadRequest(new {error = "Phone Number must contain 7 to 15 digits, or be left blank."});
        user!.DisplayName = request.DisplayName.Trim(); user.PhoneNumber = request.PhoneNumber?.Trim(); user.Designation = request.Designation?.Trim();
        try {await db.SaveChangesAsync(ct);} catch (DbUpdateConcurrencyException) {return Conflict(new {error = "Your account changed. Reload your profile and try again."});}
        return Ok(await Describe(user, ct));
    }

    [HttpPut("profile/photo"), Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile photo, CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var type = extension == ".png" ? "image/png" : "image/jpeg";
        if (photo.Length is <= 0 or > 5 * 1024 * 1024 || extension is not (".jpg" or ".jpeg" or ".png") || !photo.ContentType.Equals(type, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new {error = "Choose a JPG, JPEG or PNG image up to 5 MB."});
        StoredProjectPhoto saved;
        try {saved = await storage.SaveProfilePhotoAsync(photo, user!.Id, ct);}
        catch (ProjectPhotoValidationException) {return BadRequest(new {error = "The uploaded image format or content is invalid."});}
        var previous = user!.ProfilePhotoPath; user.ProfilePhotoPath = saved.RelativePath;
        try {await db.SaveChangesAsync(ct);} catch {await storage.DeleteAsync(saved.RelativePath, CancellationToken.None); throw;}
        if (previous is not null) await storage.DeleteAsync(previous, ct);
        return Ok(await Describe(user, ct));
    }

    [HttpDelete("profile/photo")]
    public async Task<IActionResult> RemovePhoto(CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        var previous = user!.ProfilePhotoPath; user.ProfilePhotoPath = null; await db.SaveChangesAsync(ct);
        if (previous is not null) await storage.DeleteAsync(previous, ct);
        return Ok(await Describe(user, ct));
    }

    [HttpGet("profile/photo")]
    public async Task<IActionResult> Photo(CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        if (user!.ProfilePhotoPath is null) return NotFound();
        var content = await storage.OpenReadAsync(user.ProfilePhotoPath, ct); if (content is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Stream, content.ContentType);
    }

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("AnonymousAuth")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == Normalize(request.Email), ct);
        if (Eligible(user) && HasLocalPassword(user!))
        {
            var link = await Issue(user!, "PasswordReset", null, ct);
            if (link is not null && !mailQueue.TryEnqueue(new AccountMail(user!.Email, user.DisplayName, "Reset your Ex-Survey password", "Open this link to reset your password. It expires in 20 minutes and can be used once:\n\n" + link + "\n\nIf you did not request this, ignore this email.")))
                logger.LogWarning("Account email queue is full.");
        }
        return Ok(new {message = GenericResetMessage});
    }

    public const string GenericCodeMessage = "If an account exists with this email, a verification code has been sent.";

    [HttpPost("forgot-password-code"), AllowAnonymous, EnableRateLimiting("AnonymousAuth")]
    public async Task<IActionResult> ForgotPasswordCode(EmailRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == Normalize(request.Email), ct);
        if (Eligible(user) && HasLocalPassword(user!))
        {
            var code = await Issue(user!, "PasswordResetCode", null, ct, useCode: true);
            if (code is not null && !mailQueue.TryEnqueue(new AccountMail(user!.Email, user.DisplayName, "Your Ex-Survey password reset code",
                "Return to the Forgot Password screen and enter this verification code:\n\n" + code + "\n\nIt expires in 20 minutes and can be used once. Never share this code. If you did not request it, ignore this email.")))
                logger.LogWarning("Account email queue is full.");
        }
        return Ok(new {message = GenericCodeMessage});
    }

    [HttpPost("reset-password-code"), AllowAnonymous, EnableRateLimiting("AnonymousAuth")]
    public Task<IActionResult> ResetPasswordCode(ResetPasswordCodeRequest request, CancellationToken ct)
        => CompletePasswordReset(request.NewPassword, request.ConfirmPassword, () => ReadCode(request.Email, request.Code, ct), ct);

    private async Task<AccountActionToken?> ReadCode(string emailAddress, string code, CancellationToken ct)
    {
        var normalized = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (normalized.Length != 16 || normalized.Any(c => !Uri.IsHexDigit(c))) return null;
        var hash = Hash(normalized); var now = DateTimeOffset.UtcNow; var email = Normalize(emailAddress);
        return await db.AccountActionTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == "PasswordResetCode" && t.ConsumedAt == null && t.ExpiresAt > now && db.Users.Any(u => u.Id == t.UserId && u.Email == email), ct);
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("AnonymousAuth")]
    public Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        => CompletePasswordReset(request.NewPassword, request.ConfirmPassword, () => ReadToken(request.Token, "PasswordReset", ct), ct);

    private async Task<IActionResult> CompletePasswordReset(string newPassword, string confirmPassword, Func<Task<AccountActionToken?>> readToken, CancellationToken ct)
    {
        if (!ValidPassword(newPassword) || newPassword != confirmPassword) return BadRequest(new {error = "Enter a valid password and a matching confirmation."});
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var token = await readToken();
        var user = token is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (!Eligible(user) || !HasLocalPassword(user!) || token!.AuthVersion != user!.AuthVersion) return BadRequest(new {error = "This code or link is invalid or expired. Request a new one."});
        user.PasswordHash = hasher.HashPassword(user, newPassword); user.AuthVersion++;
        await ConsumeAll(user.Id, ct);
        try {await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);}
        catch (DbUpdateConcurrencyException) {return BadRequest(new {error = "This link is no longer valid. Request a new link."});}
        await Notify(user.Email, user.DisplayName, "Your Ex-Survey password was changed", "Your password was changed and existing online sessions were signed out. If you did not request this change, contact your administrator.", ct);
        return Ok(new {message = "Password reset successfully. Sign in with your new password."});
    }

    [HttpPost("change-password"), EnableRateLimiting("AnonymousAuth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        if (!HasLocalPassword(user!)) return BadRequest(new {error = "Manage your password through your organization's identity provider."});
        if (!PasswordMatches(user!, request.CurrentPassword)) return BadRequest(new {error = "Current password is incorrect."});
        if (!ValidPassword(request.NewPassword) || request.NewPassword != request.ConfirmPassword) return BadRequest(new {error = "Enter a valid password and a matching confirmation."});
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        user!.PasswordHash = hasher.HashPassword(user, request.NewPassword); user.AuthVersion++;
        await ConsumeAll(user.Id, ct);
        try {await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);}
        catch (DbUpdateConcurrencyException) {return Conflict(new {error = "Your account changed. Sign in again."});}
        await Notify(user.Email, user.DisplayName, "Your Ex-Survey password was changed", "Your password was changed and existing online sessions were signed out. If this was not you, contact your administrator.", ct);
        return Ok(new {message = "Password changed. Sign in again with your new password."});
    }

    [HttpPost("email-change"), EnableRateLimiting("AnonymousAuth")]
    public async Task<IActionResult> RequestEmailChange(ChangeEmailRequest request, CancellationToken ct)
    {
        var user = await Current(ct); if (!Eligible(user)) return Unauthorized();
        if (!HasLocalPassword(user!)) return BadRequest(new {error = "Manage your login email through your organization's identity provider."});
        if (!PasswordMatches(user!, request.CurrentPassword)) return BadRequest(new {error = "Current password is incorrect."});
        var next = Normalize(request.Email); if (next == user!.Email) return BadRequest(new {error = "Enter a different email address."});
        if (await db.Users.AnyAsync(u => u.Email == next, ct)) return Conflict(new {error = "This email address cannot be used."});
        var link = await Issue(user, "EmailChange", next, ct);
        if (link is null) return StatusCode(429, new {error = "Wait before requesting another verification email."});
        var delivered = await Notify(next, user.DisplayName, "Verify your Ex-Survey email address", "Verify your new login email using this single-use link within 20 minutes:\n\n" + link, ct);
        await Notify(user.Email, user.DisplayName, "Ex-Survey email change requested", "A change of your login email to " + next + " was requested. Your current email remains active until verification completes. Contact your administrator if this was not you.", ct);
        if (!delivered) return StatusCode(503, new {error = "Verification email could not be delivered. Your current login email is unchanged. Try again later or contact your administrator."});
        return Ok(await Describe(user, ct));
    }

    [HttpPost("verify-email"), AllowAnonymous, EnableRateLimiting("AnonymousAuth")]
    public async Task<IActionResult> VerifyEmail(TokenRequest request, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var token = await ReadToken(request.Token, "EmailChange", ct);
        var user = token is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (!Eligible(user) || token!.AuthVersion != user!.AuthVersion || token.NewEmail is null) return BadRequest(new {error = "This link is invalid or expired."});
        if (await db.Users.AnyAsync(u => u.Email == token.NewEmail && u.Id != user!.Id, ct)) return Conflict(new {error = "This email address cannot be used. Request another change."});
        var previous = user!.Email; user.Email = token.NewEmail; user.AuthVersion++;
        await ConsumeAll(user.Id, ct);
        try {await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);}
        catch (DbUpdateException) {return Conflict(new {error = "This change could not be completed. Request a new verification link."});}
        await Notify(previous, user.DisplayName, "Your Ex-Survey login email changed", "Your login email was changed after verification. Contact your administrator if you did not authorize this change.", ct);
        return Ok(new {message = "Email verified. Sign in using your new email address."});
    }

    private async Task<string?> Issue(User user, string purpose, string? newEmail, CancellationToken ct, bool useCode = false)
    {
        var now = DateTimeOffset.UtcNow;
        var recent = await db.AccountActionTokens.Where(t => t.UserId == user.Id && t.CreatedAt > now.AddHours(-1)).ToListAsync(ct);
        if (recent.Count >= 5 || recent.Any(t => t.Purpose == purpose && t.CreatedAt > now.AddMinutes(-1))) return null;
        var origin = config["AccountSecurity:PublicAppUrl"];
        if (string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment()) origin = "http://localhost:3000/";
        Uri? uri = null;
        if (!useCode && (!Uri.TryCreate(origin, UriKind.Absolute, out uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            (uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http" && uri.IsLoopback))))
        {logger.LogWarning("Account email link origin is not configured correctly."); return null;}
        var raw = useCode ? Convert.ToHexString(RandomNumberGenerator.GetBytes(8)) : Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        db.AccountActionTokens.Add(new AccountActionToken {UserId = user.Id, Purpose = purpose, TokenHash = Hash(raw), NewEmail = newEmail, AuthVersion = user.AuthVersion, CreatedAt = now, ExpiresAt = now.AddMinutes(20)});
        try {await db.SaveChangesAsync(ct);} catch (DbUpdateConcurrencyException) {return null;}
        if (useCode) return string.Join("-", Enumerable.Range(0, 4).Select(i => raw.Substring(i * 4, 4)));
        return uri!.AbsoluteUri.TrimEnd('/') + "/#" + (purpose == "PasswordReset" ? "reset-password" : "verify-email") + "?token=" + Uri.EscapeDataString(raw);
    }

    private Task<AccountActionToken?> ReadToken(string token, string purpose, CancellationToken ct)
    {
        if (token.Length != 43) return Task.FromResult<AccountActionToken?>(null);
        var hash = Hash(token); var now = DateTimeOffset.UtcNow;
        return db.AccountActionTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose && t.ConsumedAt == null && t.ExpiresAt > now, ct);
    }
    private async Task ConsumeAll(Guid userId, CancellationToken ct)
    {
        foreach (var token in await db.AccountActionTokens.Where(t => t.UserId == userId && t.ConsumedAt == null).ToListAsync(ct)) token.ConsumedAt = DateTimeOffset.UtcNow;
    }
    private async Task<bool> Notify(string recipient, string name, string subject, string body, CancellationToken ct)
    {
        try {await email.SendAccountMessageAsync(recipient, name, subject, body, ct); return true;}
        catch (Exception) {logger.LogWarning("Account notification delivery failed for user action. Message content is intentionally omitted."); return false;}
    }
}

public record ProfileWrite([Required, StringLength(200)] string DisplayName, [StringLength(40), RegularExpression(@"^[0-9+()\- .]*$", ErrorMessage = "Enter a valid phone number.")] string? PhoneNumber, [StringLength(150)] string? Designation);
public record EmailRequest([Required, EmailAddress, StringLength(320)] string Email);
public record TokenRequest([Required, StringLength(128)] string Token);
public record ResetPasswordRequest([Required, StringLength(128)] string Token, [Required, StringLength(256)] string NewPassword, [Required, StringLength(256)] string ConfirmPassword);
public record ChangePasswordRequest([Required, StringLength(256)] string CurrentPassword, [Required, StringLength(256)] string NewPassword, [Required, StringLength(256)] string ConfirmPassword);
public record ChangeEmailRequest([Required, EmailAddress, StringLength(320)] string Email, [Required, StringLength(256)] string CurrentPassword);

public record ResetPasswordCodeRequest([Required, EmailAddress, StringLength(320)] string Email, [Required, StringLength(32)] string Code, [Required, StringLength(256)] string NewPassword, [Required, StringLength(256)] string ConfirmPassword);
