using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public static class DevelopmentDatabaseInitializer
{
    public static async Task SeedAdminIfEmptyAsync(SurveyDbContext db,
        IPasswordHasher<User> hasher, IConfiguration configuration)
    {
        if (await db.Users.AnyAsync()) return;
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        BootstrapAccountProvisioner.ValidateConfiguration("BootstrapAdmin", email, password);
        await BootstrapAccountProvisioner.ProvisionAsync(db, hasher,
            string.IsNullOrWhiteSpace(email) ? "admin@abc.com" : email,
            string.IsNullOrWhiteSpace(password) ? "admin@123" : password,
            configuration["BootstrapAdmin:DisplayName"] ?? "Development Administrator", UserRole.Admin);
    }
}
