using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public enum DevelopmentAccountSeedResult { Created, AlreadyExists, RoleConflict }

public static class BootstrapAccountProvisioner
{
    // Demo accounts are initial fixtures, not an authority over later user-management changes.
    public static async Task<DevelopmentAccountSeedResult> CreateDevelopmentAccountIfMissingAsync(
        SurveyDbContext db, IPasswordHasher<User> passwordHasher, string email, string password,
        string displayName, UserRole role, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
            return existing.Role == role
                ? DevelopmentAccountSeedResult.AlreadyExists
                : DevelopmentAccountSeedResult.RoleConflict;

        await ProvisionAsync(db, passwordHasher, email, password, displayName, role, cancellationToken);
        return DevelopmentAccountSeedResult.Created;
    }

    public static void ValidateConfiguration(string section, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) != string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"{section}:Email and {section}:Password must both be set.");
    }

    public static async Task ProvisionAsync(
        SurveyDbContext db,
        IPasswordHasher<User> passwordHasher,
        string email,
        string password,
        string displayName,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedDisplayName = displayName.Trim();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                DisplayName = normalizedDisplayName,
                PasswordHash = string.Empty,
                Role = role,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        else if (user.Role != role)
        {
            throw new InvalidOperationException($"Bootstrap {role} email belongs to a non-{role} user.");
        }
        else
        {
            user.DisplayName = normalizedDisplayName;
            user.IsActive = true;
            user.ApprovalStatus = ApprovalStatus.Approved;
            if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
                user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
