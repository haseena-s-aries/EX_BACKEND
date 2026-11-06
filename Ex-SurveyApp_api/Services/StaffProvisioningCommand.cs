using System.Data;
using System.Net.Mail;
using System.Text;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

internal static class StaffProvisioningCommand
{
    public static async Task<int> RunAsync(WebApplicationBuilder builder, UserRole role, string command)
    {
        if (Console.IsInputRedirected)
            return Fail($"Run {command} in an interactive terminal.");

        try
        {
            var connection = SurveyDatabaseConfiguration.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
            var target = new SqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(target.InitialCatalog))
                return Fail("The connection string must name the target database.");

            Console.WriteLine($"SQL Server: {target.DataSource} | Database: {target.InitialCatalog}");
            Console.Write("Type the target database name to continue: ");
            if (!string.Equals(Console.ReadLine()?.Trim(), target.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                return Fail("Database confirmation did not match.");

            Console.Write($"{role} email: ");
            var email = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (email is null || email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
                return Fail("Enter a valid email address.");

            var password = ReadPassword($"{role} password: ");
            if (string.IsNullOrWhiteSpace(password))
                return Fail("A password is required.");
            if (password != ReadPassword("Confirm password: "))
                return Fail("Passwords did not match.");

            await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(connection).Options);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.Users.AnyAsync(u => u.Email == email))
                return Fail("That email already belongs to an account.");

            await BootstrapAccountProvisioner.ProvisionAsync(db, new PasswordHasher<User>(), email, password,
                role == UserRole.Admin ? "Live Administrator" : "Live Surveyor", role);
            await transaction.CommitAsync();
            Console.WriteLine($"Created approved {role} {email}.");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail($"{role} provisioning failed ({ex.GetBaseException().Message}).");
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                password.Length--;
            else if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }
    }
}
