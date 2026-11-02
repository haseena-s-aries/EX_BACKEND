using Microsoft.Data.SqlClient;

internal static class LocalSqlDiagnostics
{
    private const string TestDatabaseName = "ExSurveySprint2_Test";

    public static async Task PrepareIsolatedTestDatabaseAsync()
    {
        const string master = "Server=(localdb)\\ExSurveySprint2;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=10;Application Name=ExSurvey-LocalDB-Rehearsal";
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        await using (var verify = connection.CreateCommand())
        {
            verify.CommandText = "SELECT @@VERSION, CAST(CONNECTIONPROPERTY('auth_scheme') AS nvarchar(30)), CAST(c.encrypt_option AS nvarchar(10)) FROM sys.dm_exec_connections c WHERE c.session_id = @@SPID";
            await using var reader = await verify.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Could not verify SQL version/authentication/encryption on the diagnostic session.");
            Console.WriteLine($"SQL_SERVER_VERSION={reader.GetString(0).Split('\n')[0]}");
            Console.WriteLine($"AUTH_SCHEME={reader.GetString(1)}; ENCRYPTED={reader.GetString(2)}");
        }
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"IF DB_ID(N'{TestDatabaseName}') IS NULL CREATE DATABASE [{TestDatabaseName}]";
            await create.ExecuteNonQueryAsync();
        }
        Console.WriteLine($"ISOLATED_TEST_DATABASE_READY={TestDatabaseName}; contains only newly-created synthetic test data.");
    }

    public static async Task Run(string? configuredConnection = null)
    {
        const string localOnlyConnection = "Server=localhost;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=5;Application Name=ExSurvey-Local-Sql-Diagnostic";
        var connectionString = configuredConnection ?? localOnlyConnection;
        await using var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            Console.WriteLine("LOCAL_SQL_DIAGNOSTIC=Connected with encryption required and certificate validation enabled.");
        }
        catch (SqlException error)
        {
            Console.WriteLine($"LOCAL_SQL_DIAGNOSTIC=SqlException Number={error.Number}; State={error.State}; Message={error.Message}");
            Console.WriteLine("No query was executed and no database was modified.");
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            Console.WriteLine($"LOCAL_SQL_DIAGNOSTIC={error.GetType().Name}; Message={error.Message}");
            Console.WriteLine("No query was executed and no database was modified.");
            Environment.ExitCode = 0;
        }
    }
}
