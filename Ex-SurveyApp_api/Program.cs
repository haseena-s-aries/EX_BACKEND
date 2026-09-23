using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
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
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
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
                    !context.Principal!.IsInRole(user.Role.ToString()))
                    context.Fail("User is inactive, unapproved, or role has changed.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDbContext<SurveyDbContext>(options =>
{
    if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("UseInMemoryDatabase"))
        options.UseInMemoryDatabase("ExSurveyDevelopment");
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("SurveyDatabase")
            ?? throw new InvalidOperationException("Connection string 'SurveyDatabase' is required."));
});

var app = builder.Build();

var adminEmail = app.Configuration["BootstrapAdmin:Email"];
var adminPassword = app.Configuration["BootstrapAdmin:Password"];
var adminDisplayName = app.Configuration["BootstrapAdmin:DisplayName"] ?? "Administrator";
if (string.IsNullOrWhiteSpace(adminEmail) != string.IsNullOrWhiteSpace(adminPassword))
    throw new InvalidOperationException("BootstrapAdmin:Email and BootstrapAdmin:Password must both be set.");
if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
    var normalizedAdminEmail = adminEmail.Trim().ToLowerInvariant();
    var admin = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedAdminEmail);
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    if (admin is null)
    {
        admin = new User
        {
            Email = normalizedAdminEmail,
            DisplayName = adminDisplayName.Trim(),
            PasswordHash = string.Empty,
            Role = UserRole.Admin,
            ApprovalStatus = ApprovalStatus.Approved
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
        db.Users.Add(admin);
    }
    else if (admin.Role != UserRole.Admin)
    {
        throw new InvalidOperationException("Bootstrap admin email belongs to a non-admin user.");
    }
    else
    {
        admin.DisplayName = adminDisplayName.Trim();
        admin.IsActive = true;
        admin.ApprovalStatus = ApprovalStatus.Approved;
        if (passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, adminPassword) ==
            PasswordVerificationResult.Failed)
            admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
    }

    await db.SaveChangesAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
