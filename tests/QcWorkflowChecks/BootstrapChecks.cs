using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class BootstrapChecks
{
    public static async Task Run()
    {
        var options = new DbContextOptionsBuilder<SurveyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new SurveyDbContext(options);
        var hasher = new PasswordHasher<User>();

        BootstrapAccountProvisioner.ValidateConfiguration("BootstrapManager", null, null);
        ExpectInvalidOperation(
            () => BootstrapAccountProvisioner.ValidateConfiguration("BootstrapManager", "manager@example.test", null),
            "BootstrapManager:Email and BootstrapManager:Password must both be set.");

        await BootstrapAccountProvisioner.ProvisionAsync(db, hasher, " Manager@Example.Test ", "test-password",
            " Manager One ", UserRole.Manager);
        var manager = await db.Users.SingleAsync();
        var originalId = manager.Id;
        Check(manager.Email == "manager@example.test", "Manager email is normalized");
        Check(manager.Role == UserRole.Manager && manager.ApprovalStatus == ApprovalStatus.Approved && manager.IsActive,
            "Bootstrap Manager is active and approved");
        Check(hasher.VerifyHashedPassword(manager, manager.PasswordHash, "test-password") != PasswordVerificationResult.Failed,
            "Bootstrap password is stored as a valid hash");

        await BootstrapAccountProvisioner.ProvisionAsync(db, hasher, "manager@example.test", "test-password",
            "Manager Updated", UserRole.Manager);
        Check(await db.Users.CountAsync() == 1 && manager.Id == originalId && manager.DisplayName == "Manager Updated",
            "Repeated bootstrap is idempotent and updates the existing account");

        await BootstrapAccountProvisioner.ProvisionAsync(db, hasher, "surveyor1@example.test", "surveyor-password",
            "Development Surveyor 1", UserRole.Surveyor);
        await BootstrapAccountProvisioner.ProvisionAsync(db, hasher, "client@example.test", "client-password",
            "Development Client", UserRole.Client);
        Check(await db.Users.CountAsync() == 3 &&
              (await db.Users.SingleAsync(x => x.Email == "surveyor1@example.test")).Role == UserRole.Surveyor &&
              (await db.Users.SingleAsync(x => x.Email == "client@example.test")).Role == UserRole.Client,
            "Development Surveyor and Client bootstrap accounts use the expected roles");

        await db.Users.AddAsync(new User
        {
            Email = "admin@example.test", DisplayName = "Existing Admin", PasswordHash = "x", Role = UserRole.Admin
        });
        await db.SaveChangesAsync();
        try
        {
            await BootstrapAccountProvisioner.ProvisionAsync(db, hasher, "admin@example.test", "test-password",
                "Conflict", UserRole.Manager);
            throw new InvalidOperationException("Expected role-conflict validation to fail.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("non-Manager user", StringComparison.Ordinal))
        {
            Console.WriteLine("PASS: conflicting bootstrap role is rejected");
        }

        var conflicting = await db.Users.SingleAsync(x => x.Email == "admin@example.test");
        var conflictId = conflicting.Id;
        var result = await BootstrapAccountProvisioner.CreateDevelopmentAccountIfMissingAsync(
            db, hasher, " Admin@Example.Test ", "replacement-password", "Demo Manager", UserRole.Manager);
        db.ChangeTracker.Clear();
        conflicting = await db.Users.SingleAsync(x => x.Id == conflictId);
        Check(result == DevelopmentAccountSeedResult.RoleConflict && conflicting.Role == UserRole.Admin &&
              conflicting.PasswordHash == "x" && conflicting.DisplayName == "Existing Admin",
            "Optional demo role conflicts do not throw or overwrite the existing user");

        var edited = await db.Users.SingleAsync(x => x.Email == "manager@example.test");
        edited.DisplayName = "User-managed name";
        edited.IsActive = false;
        edited.ApprovalStatus = ApprovalStatus.Rejected;
        var editedHash = edited.PasswordHash;
        await db.SaveChangesAsync();
        result = await BootstrapAccountProvisioner.CreateDevelopmentAccountIfMissingAsync(
            db, hasher, edited.Email, "replacement-password", "Demo name", UserRole.Manager);
        db.ChangeTracker.Clear();
        edited = await db.Users.SingleAsync(x => x.Email == "manager@example.test");
        Check(result == DevelopmentAccountSeedResult.AlreadyExists && edited.DisplayName == "User-managed name" &&
              !edited.IsActive && edited.ApprovalStatus == ApprovalStatus.Rejected && edited.PasswordHash == editedHash,
            "Optional demo seeding preserves existing name, password, approval and active state");

        result = await BootstrapAccountProvisioner.CreateDevelopmentAccountIfMissingAsync(
            db, hasher, "new-demo@example.test", "demo-password", "New Demo", UserRole.Surveyor);
        var created = await db.Users.SingleAsync(x => x.Email == "new-demo@example.test");
        Check(result == DevelopmentAccountSeedResult.Created && created.Role == UserRole.Surveyor &&
              created.IsActive && created.ApprovalStatus == ApprovalStatus.Approved &&
              hasher.VerifyHashedPassword(created, created.PasswordHash, "demo-password") != PasswordVerificationResult.Failed,
            "Missing demo accounts are still created with an approved role and hashed password");

        Console.WriteLine("Bootstrap checks passed.");
    }

    private static void ExpectInvalidOperation(Action action, string expectedMessage)
    {
        try
        {
            action();
            throw new InvalidOperationException("Expected incomplete configuration to fail.");
        }
        catch (InvalidOperationException ex) when (ex.Message == expectedMessage)
        {
            Console.WriteLine("PASS: partial bootstrap configuration is rejected");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
        Console.WriteLine($"PASS: {message}");
    }
}
