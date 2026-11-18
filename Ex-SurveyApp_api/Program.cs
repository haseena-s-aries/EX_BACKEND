using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

if (args.Length == 1 && args[0] is ("create-admin" or "create-surveyor"))
{
    var role = args[0] == "create-admin" ? UserRole.Admin : UserRole.Surveyor;
    Environment.ExitCode = await StaffProvisioningCommand.RunAsync(WebApplication.CreateBuilder([]), role, args[0]);
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddScoped<FieldworkMutationFilter>();
builder.Services.AddScoped<FieldworkFileTransaction>();
builder.Services.AddControllers(o => o.Filters.AddService<FieldworkMutationFilter>()).AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                allowedOrigins.Concat(new[] {"http://localhost:3000", "http://10.0.2.2:3000"}).Distinct().ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddSwaggerGen(options =>
{
    ProjectUploadOpenApi.Configure(options);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExSurveyApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ExSurveyClients";
var configuredKey = builder.Configuration["Jwt:SigningKey"];
var jwtKey = string.IsNullOrWhiteSpace(configuredKey) && builder.Environment.IsDevelopment()
    ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    : configuredKey;
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes outside Development.");

builder.Services.AddSingleton(new JwtSettings(jwtIssuer, jwtAudience, jwtKey));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AccountMailQueue>();
builder.Services.AddSingleton<IAccountMailQueue>(s => s.GetRequiredService<AccountMailQueue>());
builder.Services.AddHostedService(s => s.GetRequiredService<AccountMailQueue>());
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ProjectScheduling>();
builder.Services.AddHostedService<ProjectDelayMonitor>();
builder.Services.Configure<ProjectPhotoStorageSettings>(builder.Configuration.GetSection("ProjectPhotoStorage"));
builder.Services.Configure<GeneratedReportArtifactStorageSettings>(builder.Configuration.GetSection("GeneratedReportArtifactStorage"));
builder.Services.Configure<OfflineInspectionOptions>(builder.Configuration.GetSection("OfflineInspection"));
builder.Services.Configure<EquipmentDocumentOptions>(builder.Configuration.GetSection("EquipmentDocuments"));
builder.Services.AddSingleton<IProjectPhotoStorage, ProjectPhotoStorage>();
builder.Services.AddSingleton<IGeneratedReportArtifactStorage, GeneratedReportArtifactStorage>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                {
                    context.Fail("Invalid user.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<SurveyDbContext>();
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (user is null || !user.IsActive || user.ApprovalStatus != ApprovalStatus.Approved ||
                    !context.Principal!.IsInRole(user.Role.ToString()) || !TokenService.IsCurrentSession(context.Principal!, user))
                    context.Fail("User is inactive, unapproved, or role has changed.");
            }
        };
    });

builder.Services.AddTenantFoundation(builder.Configuration);
builder.Services.AddInspectionActivityFoundation(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "300";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("AnonymousAuth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
// SQL Server only; runtime and EF tools share the same resolver.
var databaseConnection = SurveyDatabaseConfiguration.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddDbContext<SurveyDbContext>(options => options.UseSqlServer(databaseConnection));
builder.Services.AddProblemDetails();

var app = builder.Build();
var databaseTarget = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(databaseConnection);
app.Logger.LogInformation("Database provider: SQL Server; server: {Server}; database: {Database}",
    databaseTarget.DataSource, databaseTarget.InitialCatalog);

// Migrations must complete before any account or checklist queries.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await DevelopmentDatabaseInitializer.SeedAdminIfEmptyAsync(db,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), app.Configuration);
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Development database initialization failed for {Server}/{Database}",
            databaseTarget.DataSource, databaseTarget.InitialCatalog);
        throw;
    }
}

var adminEmail = app.Configuration["BootstrapAdmin:Email"];
var adminPassword = app.Configuration["BootstrapAdmin:Password"];
var adminDisplayName = app.Configuration["BootstrapAdmin:DisplayName"] ?? "Administrator";
var managerEmail = app.Configuration["BootstrapManager:Email"];
var managerPassword = app.Configuration["BootstrapManager:Password"];
var managerDisplayName = app.Configuration["BootstrapManager:DisplayName"] ?? "Manager";
BootstrapAccountProvisioner.ValidateConfiguration("BootstrapAdmin", adminEmail, adminPassword);
BootstrapAccountProvisioner.ValidateConfiguration("BootstrapManager", managerEmail, managerPassword);
if ((!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)) ||
    (!string.IsNullOrWhiteSpace(managerEmail) && !string.IsNullOrWhiteSpace(managerPassword)))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        await BootstrapAccountProvisioner.ProvisionStartupAsync(db, passwordHasher, "Admin", adminEmail, adminPassword,
            adminDisplayName, UserRole.Admin);
    if (!string.IsNullOrWhiteSpace(managerEmail) && !string.IsNullOrWhiteSpace(managerPassword))
        await BootstrapAccountProvisioner.ProvisionStartupAsync(db, passwordHasher, "Manager", managerEmail, managerPassword,
            managerDisplayName, UserRole.Manager);
}

// Optional local demo accounts. Create missing fixtures; preserve later account edits.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("DevelopmentAccounts:Enabled"))
{
    var accounts = new[]
    {
        (Section: "Manager", Role: UserRole.Manager, FallbackName: "Development Manager"),
        (Section: "Surveyor1", Role: UserRole.Surveyor, FallbackName: "Development Surveyor 1"),
        (Section: "Client", Role: UserRole.Client, FallbackName: "Development Client")
    }.Select(x => (
        x.Role,
        Email: app.Configuration[$"DevelopmentAccounts:{x.Section}:Email"],
        Password: app.Configuration[$"DevelopmentAccounts:{x.Section}:Password"],
        DisplayName: app.Configuration[$"DevelopmentAccounts:{x.Section}:DisplayName"] ?? x.FallbackName,
        ConfigSection: $"DevelopmentAccounts:{x.Section}"))
        .ToArray();

    foreach (var account in accounts)
    {
        BootstrapAccountProvisioner.ValidateConfiguration(account.ConfigSection, account.Email, account.Password);
        if (string.IsNullOrWhiteSpace(account.Email) || string.IsNullOrWhiteSpace(account.Password))
            throw new InvalidOperationException($"{account.ConfigSection}:Email and Password are required when DevelopmentAccounts:Enabled is true.");
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    foreach (var account in accounts)
    {
        var result = await BootstrapAccountProvisioner.CreateDevelopmentAccountIfMissingAsync(db, passwordHasher, account.Email!, account.Password!,
            account.DisplayName, account.Role);
        if (result == DevelopmentAccountSeedResult.RoleConflict)
            app.Logger.LogWarning(
                "Skipping {Section}: email {Email} already belongs to another role. Existing account was preserved. Use a different demo email or disable DevelopmentAccounts:Enabled.",
                account.ConfigSection, account.Email);
    }
}

// The explicitly isolated integration profile supplies its own labeled synthetic checklist fixture.
// Other environments add missing defaults while preserving existing checklist versions.
if (!(app.Environment.IsDevelopment() &&
      app.Configuration.GetValue<bool>("DevelopmentChecklistSeed:DisableStartupSeed")))
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
    await DefaultChecklistSeeder.SeedAsync(db);
}

// Configure the HTTP request pipeline.
// Enable Swagger in all environments.
// Secure access to the API separately if required.
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "v1/swagger.json",
        "Ex Survey API V1");

    options.RoutePrefix = "swagger";
});
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", (HttpContext context) =>
    Results.Redirect($"{context.Request.PathBase}/swagger/index.html"))
    .ExcludeFromDescription();
app.MapControllers();

app.Run();
