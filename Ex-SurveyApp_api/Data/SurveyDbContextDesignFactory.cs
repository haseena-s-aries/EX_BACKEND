using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ex_SurveyApp_api.Data;

public sealed class SurveyDbContextDesignFactory : IDesignTimeDbContextFactory<SurveyDbContext>
{
    public SurveyDbContext CreateDbContext(string[] args)
    {
        // Find the API settings whether EF is invoked from the API or solution directory.
        var root = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(root, "appsettings.json")))
            root = Path.Combine(root, "Ex-SurveyApp_api");
        if (!File.Exists(Path.Combine(root, "appsettings.json")))
            throw new InvalidOperationException("Run EF tools from the API or solution directory.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(SurveyDbContext).Assembly.GetName().Name,
            ContentRootPath = root,
            EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
        });
        var connection = SurveyDatabaseConfiguration.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
        return new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(connection).Options);
    }
}
