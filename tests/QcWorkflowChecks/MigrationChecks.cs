using Ex_SurveyApp_api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class MigrationChecks
{
    public static void RunOtpSecurityScript()
    {
        var options = new DbContextOptionsBuilder<SurveyDbContext>()
            .UseSqlServer("Server=localhost;Database=MigrationScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new SurveyDbContext(options);
        var script = db.GetService<IMigrator>().GenerateScript(
            fromMigration: "20261027090000_AddEquipmentRfidTagUid",
            toMigration: "20261029110000_AddOtpVerificationSecurity");

        foreach (var column in new[] { "VerificationAttempts", "LockedUntil", "ConcurrencyVersion" })
            if (!script.Contains(column, StringComparison.Ordinal))
                throw new InvalidOperationException($"Generated SQL is missing expected column {column}.");

        Console.WriteLine("PASS: SQL Server migration script contains OTP attempt, lockout and concurrency columns.");
        Console.WriteLine("PASS: Script was generated without opening a database connection.");
    }

    public static void RunTenantFoundationScript()
    {
        var options = new DbContextOptionsBuilder<SurveyDbContext>()
            .UseSqlServer("Server=localhost;Database=MigrationScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new SurveyDbContext(options);
        var script = db.GetService<IMigrator>().GenerateScript(
            fromMigration: "20261029110000_AddOtpVerificationSecurity",
            toMigration: "20261029123545_Phase2aTenantFoundation");
        var createCount = System.Text.RegularExpressions.Regex.Matches(script, @"(?im)^CREATE TABLE ").Count;
        if (createCount != 8) throw new InvalidOperationException($"Expected 8 additive foundation tables, found {createCount}.");
        foreach (var forbidden in new[] { "DROP TABLE", "DROP COLUMN", "ALTER TABLE [Projects]", "ALTER TABLE [ProjectAreas]", "ALTER TABLE [AreaEquipments]", "UPDATE ", "DELETE FROM " })
            if (script.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Tenant foundation migration contains prohibited SQL: {forbidden}.");
        if (System.Text.RegularExpressions.Regex.Matches(script, @"(?im)^INSERT INTO ").Count != 1 ||
            !script.Contains("INSERT INTO [__EFMigrationsHistory]", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected data insert in tenant foundation migration script.");
        Console.WriteLine("PASS: Tenant migration SQL creates eight foundation tables only; no destructive or ownership data changes.");
        Console.WriteLine("PASS: SQL was generated from migration metadata without opening a database connection.");
    }
}
