# Development database alignment — 3 November 2026

## Findings before this change

- Runtime `Program.cs` selected SQL Server from `ConnectionStrings:SurveyDatabase`, except when Development + `UseInMemoryDatabase=true` selected `ExSurveyDevelopment` (InMemory).
- `appsettings.json`: no database connection. `appsettings.Development.json`: `localhost / ExApp`. `appsettings.Production.json`: `localhost / ExApp` (unchanged).
- The `ExSurveySprint2LocalDB` launch profile overrode the connection with `(localdb)\ExSurveySprint2 / ExSurveySprint2_Test`.
- Local user secrets at `C:/Users/AL414/AppData/Roaming/Microsoft/UserSecrets/Ex-SurveyApp-API-Development/secrets.json`: no database connection or InMemory override found; `DevelopmentAccounts:Enabled=true`. No connection, database-target, or environment override was found in the inspecting shell. A separately launched Visual Studio process can have different inherited environment values.
- EF design factory bypassed runtime settings and selected `(localdb)\MSSQLLocalDB / ExSurveyMigrationDesignOnly` by default. `EXSURVEY_MIGRATION_TARGET` could select two other databases. That separate target switch has been removed.
- Read-only inspection of the allowed database found migration `20261103074858_InitialCreate`, zero Users and zero Projects. Recreating the schema does not restore users, passwords or old project IDs.
- InMemory calls elsewhere are explicit isolated tests. Other UseSqlServer calls in tests are explicit test/diagnostic helpers, not runtime fallbacks. They were not executed against other databases.

## What changed

SQL Server is now the only runtime provider. Runtime and EF load the same configuration and call the same connection resolver. The factory loads API user secrets using the API assembly identity, without building/running the API or seeding data. Development accepts only `(localdb)\MSSQLLocalDB / ExSurveyMigrationDesignOnly`; an unexpected secret, command-line, or environment override fails before connecting. Production retains its existing configured target and receives no automatic startup migrations.

Development awaits `Database.MigrateAsync()` before querying/seeding anything. An empty Users table receives an active, approved Admin (`admin@abc.com` / `admin@123`, unless a complete BootstrapAdmin pair is configured). Passwords use the existing Identity password hasher. The empty-database initializer does not reset an existing user's password. Existing explicitly configured bootstrap/demo behavior remains in place.

The project details action already had a null-to-404 check. An early scoped existence check now returns 404 before loading the full graph for an old or inaccessible ID. Unexpected request exceptions use the standard Problem Details exception handler and are logged; schema failures are not disguised as 404.

## Exceptions and process shutdown: confirmed versus unknown

The only captured process-level failure in the available log is `output/local-api-diagnosis.log:38`:

```text
System.IO.IOException: Failed to bind to address https://127.0.0.1:7288: address already in use.
 ---> Microsoft.AspNetCore.Connections.AddressInUseException:
 Only one usage of each socket address (protocol/network address/port) is normally permitted.
```

This is a startup port collision, not proof that a project request killed the host. Running one API instance on 7288 resolves the observed collision; no unrelated process was terminated. My verification instances were stopped after testing so Visual Studio can bind that port.

No stack trace for the reported original project 500 was available, and it could not be reproduced on the allowed migrated database. A mismatched schema is a possible explanation, not a confirmed exception. A 401 means credentials/token were rejected; it does not itself stop Kestrel. The corrected API remained alive after 401 and 404 responses. The earlier photographed `Sequence contains more than one element` checklist startup exception was addressed by the existing DefaultChecklistSeeder fix, which remains in place.

## Verification

- SQL inspection before initialization: InitialCreate applied; zero users and projects.
- API build: succeeded, zero warnings and errors.
- Fresh `dotnet ef dbcontext info --project Ex-SurveyApp_api --configuration DatabaseAlignment`: SQL Server, ExSurveyMigrationDesignOnly, (localdb)\MSSQLLocalDB.
- DatabaseStartupChecks: SQL provider even with obsolete InMemory switch, runtime/design target parity, unsafe override rejection, hashed active approved Admin, repeated initialization without reset.
- Real SQL HTTP on 7290: admin login 200, missing project 404, wrong password 401, subsequent projects request 200.
- Second startup using actual HTTPS 7288: admin login successful, missing project 404, subsequent projects request 200.
- Project HTTP regression suite: includes a missing-ID 404 assertion; uses isolated InMemory test data, not any other SQL database.
- No database was dropped. Only ExSurveyMigrationDesignOnly received startup initialization writes. Production configuration and user secrets were not edited.

## Exact changed code (current source line numbers)

The following excerpts cover this request's changes; other existing working-tree edits were preserved. InMemory references remain available for explicit tests, but the runtime branch selecting that provider was removed.

### Ex-SurveyApp_api\Program.cs — lines 129–157

```csharp
 129: // SQL Server only; runtime and EF tools share the same resolver.
 130: var databaseConnection = SurveyDatabaseConfiguration.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
 131: builder.Services.AddDbContext<SurveyDbContext>(options => options.UseSqlServer(databaseConnection));
 132: builder.Services.AddProblemDetails();
 133: 
 134: var app = builder.Build();
 135: var databaseTarget = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(databaseConnection);
 136: app.Logger.LogInformation("Database provider: SQL Server; server: {Server}; database: {Database}",
 137:     databaseTarget.DataSource, databaseTarget.InitialCatalog);
 138: 
 139: // Migrations must complete before any account or checklist queries.
 140: if (app.Environment.IsDevelopment())
 141: {
 142:     using var scope = app.Services.CreateScope();
 143:     var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
 144:     try
 145:     {
 146:         await db.Database.MigrateAsync();
 147:         await DevelopmentDatabaseInitializer.SeedAdminIfEmptyAsync(db,
 148:             scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), app.Configuration);
 149:     }
 150:     catch (Exception ex)
 151:     {
 152:         app.Logger.LogCritical(ex, "Development database initialization failed for {Server}/{Database}",
 153:             databaseTarget.DataSource, databaseTarget.InitialCatalog);
 154:         throw;
 155:     }
 156: }
 157: 
```

### Ex-SurveyApp_api\Program.cs — lines 224–225

```csharp
 224: app.UseExceptionHandler();
 225: app.UseSwagger();
```

### Ex-SurveyApp_api\Data\SurveyDatabaseConfiguration.cs — lines 1–21

```csharp
   1: using Microsoft.Data.SqlClient;
   2: 
   3: namespace Ex_SurveyApp_api.Data;
   4: 
   5: public static class SurveyDatabaseConfiguration
   6: {
   7:     public static string Resolve(IConfiguration configuration, bool development)
   8:     {
   9:         var connection = configuration.GetConnectionString("SurveyDatabase");
  10:         if (string.IsNullOrWhiteSpace(connection))
  11:             throw new InvalidOperationException("ConnectionStrings:SurveyDatabase is required. InMemory fallback is disabled.");
  12:         var target = new SqlConnectionStringBuilder(connection);
  13:         // Protect other databases from Development migration and bootstrap writes,
  14:         // including accidental overrides from secrets, environment, or launch profiles.
  15:         if (development &&
  16:             (!string.Equals(target.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
  17:              !string.Equals(target.InitialCatalog, "ExSurveyMigrationDesignOnly", StringComparison.OrdinalIgnoreCase)))
  18:             throw new InvalidOperationException("Development must target (localdb)\\MSSQLLocalDB / ExSurveyMigrationDesignOnly. Check ConnectionStrings:SurveyDatabase overrides.");
  19:         return target.ConnectionString;
  20:     }
  21: }
```

### Ex-SurveyApp_api\Data\SurveyDbContextDesignFactory.cs — lines 1–27

```csharp
   1: using Microsoft.EntityFrameworkCore;
   2: using Microsoft.EntityFrameworkCore.Design;
   3: 
   4: namespace Ex_SurveyApp_api.Data;
   5: 
   6: public sealed class SurveyDbContextDesignFactory : IDesignTimeDbContextFactory<SurveyDbContext>
   7: {
   8:     public SurveyDbContext CreateDbContext(string[] args)
   9:     {
  10:         // Find the API settings whether EF is invoked from the API or solution directory.
  11:         var root = Directory.GetCurrentDirectory();
  12:         if (!File.Exists(Path.Combine(root, "appsettings.json")))
  13:             root = Path.Combine(root, "Ex-SurveyApp_api");
  14:         if (!File.Exists(Path.Combine(root, "appsettings.json")))
  15:             throw new InvalidOperationException("Run EF tools from the API or solution directory.");
  16:         var builder = WebApplication.CreateBuilder(new WebApplicationOptions
  17:         {
  18:             Args = args,
  19:             ApplicationName = typeof(SurveyDbContext).Assembly.GetName().Name,
  20:             ContentRootPath = root,
  21:             EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
  22:                 ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
  23:         });
  24:         var connection = SurveyDatabaseConfiguration.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
  25:         return new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(connection).Options);
  26:     }
  27: }
```

### Ex-SurveyApp_api\Services\DevelopmentDatabaseInitializer.cs — lines 1–22

```csharp
   1: using Ex_SurveyApp_api.Data;
   2: using Ex_SurveyApp_api.Models;
   3: using Microsoft.AspNetCore.Identity;
   4: using Microsoft.EntityFrameworkCore;
   5: 
   6: namespace Ex_SurveyApp_api.Services;
   7: 
   8: public static class DevelopmentDatabaseInitializer
   9: {
  10:     public static async Task SeedAdminIfEmptyAsync(SurveyDbContext db,
  11:         IPasswordHasher<User> hasher, IConfiguration configuration)
  12:     {
  13:         if (await db.Users.AnyAsync()) return;
  14:         var email = configuration["BootstrapAdmin:Email"];
  15:         var password = configuration["BootstrapAdmin:Password"];
  16:         BootstrapAccountProvisioner.ValidateConfiguration("BootstrapAdmin", email, password);
  17:         await BootstrapAccountProvisioner.ProvisionAsync(db, hasher,
  18:             string.IsNullOrWhiteSpace(email) ? "admin@abc.com" : email,
  19:             string.IsNullOrWhiteSpace(password) ? "admin@123" : password,
  20:             configuration["BootstrapAdmin:DisplayName"] ?? "Development Administrator", UserRole.Admin);
  21:     }
  22: }
```

### Ex-SurveyApp_api\Controllers\ProjectController.cs — lines 124–138

```csharp
 124:     public async Task<ActionResult<ProjectResponse>> GetProject(Guid id, CancellationToken cancellationToken)
 125:     {
 126:         // Old bookmarked IDs must return 404 before loading the project graph.
 127:         if (!await AccessibleProjects().AnyAsync(x => x.Id == id, cancellationToken))
 128:             return NotFound(new { error = "Project was not found." });
 129:         var project = await AccessibleProjects()
 130:             .Include(x => x.OurCompany)
 131:             .Include(x => x.Areas)
 132:             .ThenInclude(x => x.Photos)
 133:             .Include(x => x.Areas)
 134:             .ThenInclude(x => x.EquipmentPins)
 135:             .AsSplitQuery()
 136:             .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
 137:         if (project is null)
 138:             return NotFound(new { error = "Project was not found." });
```

### Ex-SurveyApp_api\appsettings.Development.json — lines 1–20

```json
   1: {
   2:   "ConnectionStrings": {
   3:     "SurveyDatabase": "Server=(localdb)\\MSSQLLocalDB;Database=ExSurveyMigrationDesignOnly;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15"
   4:   },
   5:   "Logging": {
   6:     "LogLevel": {
   7:       "Default": "Information",
   8:       "Microsoft.AspNetCore": "Warning"
   9:     }
  10:   },
  11:   "Cors": {
  12:     "AllowedOrigins": [
  13:       "http://localhost:3000",
  14:       "https://localhost:3000",
  15:       "https://localhost:440",
  16:       "https://127.0.0.1:440",
  17:       "https://139.5.189.155:440"
  18:     ]
  19:   }
  20: }
```

### Ex-SurveyApp_api/Properties/launchSettings.json

Removed only the `ConnectionStrings__SurveyDatabase` override from `ExSurveySprint2LocalDB`; the profile now inherits the shared Development connection.

### Regression tests

Added `tests/QcWorkflowChecks/DatabaseStartupChecks.cs`, added `--database-startup-checks` and `--project-http-checks` dispatch in its `Program.cs`, and added a missing-project HTTP 404 assertion in `ProjectUploadHttpChecks.cs`.
