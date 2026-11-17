using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ex_SurveyApp_api.Data;

static class BackupInspection
{
 public static async Task Run(bool restoreApprovedAudit = false, bool readAudit = false)
 {
var output=Path.GetFullPath("artifacts/backup-schema-audit");Directory.CreateDirectory(output);
var json=new JsonSerializerOptions {WriteIndented=true};
using var modelDb=new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ExSurveyMigrationDesignOnly;Integrated Security=True;TrustServerCertificate=True").Options);
var expected=modelDb.Model.GetRelationalModel().Tables.Select(t=>new {Schema=t.Schema??"dbo",Table=t.Name,Columns=t.Columns.Select(c=>new {c.Name,c.StoreType,c.IsNullable}).ToArray()}).ToArray();
await File.WriteAllTextAsync(Path.Combine(output,"expected-schema.json"),JsonSerializer.Serialize(expected,json));
await File.WriteAllTextAsync(Path.Combine(output,"expected-migrations.json"),JsonSerializer.Serialize(modelDb.Database.GetMigrations(),json));
Console.WriteLine($"Expected schema: {expected.Length} tables, {expected.Sum(t=>t.Columns.Length)} columns.");
using var connection=new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;Application Name=ExSurveyBackupReadOnlyAudit");
await connection.OpenAsync();
foreach(var (name,sql) in new[]{("server","SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) ProductVersion"),("backup-header","RESTORE HEADERONLY FROM DISK = N'C:\\Users\\AL414\\Desktop\\ExSurveyApp.bak'"),("backup-files","RESTORE FILELISTONLY FROM DISK = N'C:\\Users\\AL414\\Desktop\\ExSurveyApp.bak'")}) {
    using var cmd=new SqlCommand(sql,connection){CommandTimeout=60};using var reader=await cmd.ExecuteReaderAsync();var rows=new List<Dictionary<string,object?>>();
    while(await reader.ReadAsync()) {var row=new Dictionary<string,object?>();for(var i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}
    await File.WriteAllTextAsync(Path.Combine(output,name+".json"),JsonSerializer.Serialize(rows,json));Console.WriteLine($"Read {name}: {rows.Count} records.");
}

if (restoreApprovedAudit)
{
    // Explicitly approved disposable audit target. Never overwrite an existing database.
    var dataPath = Path.Combine(output, "ExSurveyBackupAudit.mdf");
    var logPath = Path.Combine(output, "ExSurveyBackupAudit_log.ldf");
    if (File.Exists(dataPath) || File.Exists(logPath))
        throw new InvalidOperationException("Audit files already exist; refusing to overwrite them.");
    using var restore = new SqlCommand(@"
IF DB_ID(N'ExSurveyBackupAudit') IS NOT NULL
    THROW 51000, 'Audit database already exists; refusing to overwrite it.', 1;
RESTORE DATABASE [ExSurveyBackupAudit]
FROM DISK = N'C:\Users\AL414\Desktop\ExSurveyApp.bak'
WITH MOVE N'ExSurveyApp_MigrationTest' TO N'" + dataPath.Replace("'", "''") + @"',
     MOVE N'ExSurveyApp_MigrationTest_log' TO N'" + logPath.Replace("'", "''") + @"', RECOVERY;", connection) { CommandTimeout = 180 };
    await restore.ExecuteNonQueryAsync();
    Console.WriteLine("Restored only ExSurveyBackupAudit; no migrations applied.");
}
if (restoreApprovedAudit || readAudit)
{
    connection.ChangeDatabase("ExSurveyBackupAudit");
    foreach (var (name, sql) in new[] {
        ("actual-columns", @"SELECT s.name AS [Schema], t.name AS [Table], c.name AS [Name],
CASE WHEN ty.name IN ('nvarchar','nchar') THEN ty.name+'('+CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length/2) END+')'
WHEN ty.name IN ('varchar','char','varbinary','binary') THEN ty.name+'('+CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length) END+')'
WHEN ty.name IN ('decimal','numeric') THEN ty.name+'('+CONVERT(varchar(10),c.precision)+','+CONVERT(varchar(10),c.scale)+')'
WHEN ty.name IN ('datetime2','datetimeoffset','time') AND c.scale<>7 THEN ty.name+'('+CONVERT(varchar(10),c.scale)+')'
ELSE ty.name END AS StoreType, c.is_nullable AS IsNullable,
c.is_identity AS IsIdentity, dc.definition AS DefaultDefinition
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.column_id"),
        ("actual-migrations", "IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId"),
        ("actual-counts", "SELECT s.name AS [Schema],t.name AS [Table],SUM(p.rows) AS [RowCount] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1) WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name")
    })
    {
        using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        await File.WriteAllTextAsync(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(rows, json));
        Console.WriteLine($"Read {name}: {rows.Count} records.");
    }
}

 }
}
