using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

internal static class DatabaseStartupChecks
{
    public static async Task Run()
    {
        const string connection = @"Server=(localdb)\MSSQLLocalDB;Database=ExSurveyMigrationDesignOnly;Integrated Security=True;TrustServerCertificate=True";
        IConfiguration Config(string value) => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:SurveyDatabase"] = value, ["UseInMemoryDatabase"] = "true" }).Build();
        var config = Config(connection);
        var resolved = SurveyDatabaseConfiguration.Resolve(config, true);
        await using var runtime = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(resolved).Options);
        await using var design = new SurveyDbContextDesignFactory().CreateDbContext([]);
        if (runtime.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer" ||
            design.Database.GetDbConnection().Database != runtime.Database.GetDbConnection().Database ||
            design.Database.GetDbConnection().DataSource != runtime.Database.GetDbConnection().DataSource)
            throw new Exception("Runtime and design-time must resolve the same SQL target, regardless of the obsolete InMemory switch.");
        foreach (var bad in new[] { "Server=localhost;Database=ExApp;Integrated Security=True", connection.Replace("ExSurveyMigrationDesignOnly", "OtherDatabase") })
        {
            try { SurveyDatabaseConfiguration.Resolve(Config(bad), true); throw new Exception("Unsafe override accepted."); }
            catch (InvalidOperationException) { }
        }
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hasher = new PasswordHasher<User>();
        await DevelopmentDatabaseInitializer.SeedAdminIfEmptyAsync(db, hasher, new ConfigurationBuilder().Build());
        var admin = await db.Users.SingleAsync();
        var hash = admin.PasswordHash;
        if (admin.Email != "admin@abc.com" || admin.Role != UserRole.Admin || !admin.IsActive ||
            admin.ApprovalStatus != ApprovalStatus.Approved ||
            hasher.VerifyHashedPassword(admin, hash, "admin@123") == PasswordVerificationResult.Failed)
            throw new Exception("Empty database must receive an approved Admin with a valid hashed password.");
        await DevelopmentDatabaseInitializer.SeedAdminIfEmptyAsync(db, hasher, new ConfigurationBuilder().Build());
        if (await db.Users.CountAsync() != 1 || admin.PasswordHash != hash)
            throw new Exception("Repeated startup must preserve the seeded account.");
        Console.WriteLine("Database startup checks passed: SQL-only configuration, EF/runtime target parity, override guard, empty-database admin, repeat startup.");
    }
}
