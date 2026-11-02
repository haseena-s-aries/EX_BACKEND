using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ex_SurveyApp_api.Data;

/// <summary>
/// EF design-time provider configuration only. Migration scaffolding does not open a connection;
/// the explicit local-only database name prevents design-time commands from loading appsettings
/// or starting the application/its database seeding path.
/// </summary>
public sealed class SurveyDbContextDesignFactory : IDesignTimeDbContextFactory<SurveyDbContext>
{
    public SurveyDbContext CreateDbContext(string[] args)
    {
        var rehearsalTarget = Environment.GetEnvironmentVariable("EXSURVEY_MIGRATION_TARGET");
        var connection = rehearsalTarget switch
        {
            null or "" => "Server=(localdb)\\MSSQLLocalDB;Database=ExSurveyMigrationDesignOnly;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Application Name=ExSurvey-EF-DesignOnly",
            "ExSurveySprint2_Test" => "Server=(localdb)\\ExSurveySprint2;Database=ExSurveySprint2_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Application Name=ExSurvey-Isolated-Rehearsal",
            // Explicit local-only target matching appsettings.Development.json and the API's localhost.ExApp connection.
            // This is opt-in for migration commands; runtime configuration remains unchanged.
            "ExAppLocal" => "Server=localhost;Database=ExApp;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;Application Name=ExSurvey-Local-Development",
            _ => throw new InvalidOperationException("EXSURVEY_MIGRATION_TARGET is not an approved local development or isolated test database target.")
        };
        var options = new DbContextOptionsBuilder<SurveyDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new SurveyDbContext(options);
    }
}
