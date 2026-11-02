using Microsoft.Data.SqlClient;

internal static class SqlBackupRestore
{
    public static async Task RoundTripAsync(string databaseConnection, string databaseName)
    {
        var expectedCount = await CountActivitiesAsync(databaseConnection);
        var backupFile = Path.Combine(Path.GetTempPath(), $"{databaseName}-{Guid.NewGuid():N}.bak");
        var masterBuilder = new SqlConnectionStringBuilder(databaseConnection) { InitialCatalog = "master" };
        try
        {
            await using (var targetConnection = new SqlConnection(databaseConnection))
            {
                await targetConnection.OpenAsync();
                await using var backup = targetConnection.CreateCommand();
                backup.CommandText = $"BACKUP DATABASE [{databaseName}] TO DISK=@path WITH INIT, CHECKSUM";
                backup.Parameters.AddWithValue("@path", backupFile);
                await backup.ExecuteNonQueryAsync();
            }
            SqlConnection.ClearAllPools();
            await using (var master = new SqlConnection(masterBuilder.ConnectionString))
            {
                await master.OpenAsync();
                await using (var single = master.CreateCommand())
                {
                    single.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                    await single.ExecuteNonQueryAsync();
                }
                try
                {
                    await using var restore = master.CreateCommand();
                    restore.CommandText = $"RESTORE DATABASE [{databaseName}] FROM DISK=@path WITH REPLACE, CHECKSUM";
                    restore.Parameters.AddWithValue("@path", backupFile);
                    await restore.ExecuteNonQueryAsync();
                }
                finally
                {
                    await using var multi = master.CreateCommand();
                    multi.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL ALTER DATABASE [{databaseName}] SET MULTI_USER";
                    await multi.ExecuteNonQueryAsync();
                }
            }
            SqlConnection.ClearAllPools();
            var restoredCount = await CountActivitiesAsync(databaseConnection);
            if (expectedCount != restoredCount) throw new InvalidOperationException("SQL backup/restore verification count did not match.");
            Console.WriteLine("PASS_SQL: isolated synthetic SQL database backup/restore retained inspection activity rows.");
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    private static async Task<int> CountActivitiesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.InspectionActivities";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
