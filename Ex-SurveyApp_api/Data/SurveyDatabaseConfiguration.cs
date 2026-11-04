using Microsoft.Data.SqlClient;

namespace Ex_SurveyApp_api.Data;

public static class SurveyDatabaseConfiguration
{
    public static string Resolve(IConfiguration configuration, bool development)
    {
        var connection = configuration.GetConnectionString("SurveyDatabase");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:SurveyDatabase is required. InMemory fallback is disabled.");
        var target = new SqlConnectionStringBuilder(connection);
        // Protect other databases from Development migration and bootstrap writes,
        // including accidental overrides from secrets, environment, or launch profiles.
        if (development &&
            (!string.Equals(target.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(target.InitialCatalog, "ExSurveyMigrationDesignOnly", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Development must target (localdb)\\MSSQLLocalDB / ExSurveyMigrationDesignOnly. Check ConnectionStrings:SurveyDatabase overrides.");
        return target.ConnectionString;
    }
}
