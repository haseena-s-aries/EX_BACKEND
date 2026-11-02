using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class OtpSqlChecks
{
    public static async Task Run()
    {
        // This check deliberately targets only the local default SQL Server instance and creates
        // a unique empty database. It never reads or accepts an application/production connection.
        const string localMasterConnection = "Server=127.0.0.1;Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5;";
        var databaseName = "ExSurveyOtpTest_" + Guid.NewGuid().ToString("N");
        var created = false;
        try
        {
            await using (var master = new SqlConnection(localMasterConnection))
            {
                await master.OpenAsync();
                await using var create = master.CreateCommand();
                create.CommandText = $"CREATE DATABASE [{databaseName}]";
                await create.ExecuteNonQueryAsync();
                created = true;
            }

            var testConnection = new SqlConnectionStringBuilder(localMasterConnection)
            {
                InitialCatalog = databaseName
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(testConnection).Options;

            await using (var migrateDb = new SurveyDbContext(options))
            {
                await migrateDb.Database.MigrateAsync();
                var applied = await migrateDb.Database.GetAppliedMigrationsAsync();
                Check(applied.Contains("20261029110000_AddOtpVerificationSecurity"),
                    "OTP security migration applies to a newly created isolated SQL Server database");
            }

            var now = DateTimeOffset.UtcNow;
            var email = $"otp-{Guid.NewGuid():N}@example.test";
            await using (var lockDb = new SurveyDbContext(options))
            {
                var otp = new RegistrationOtp
                {
                    Email = email, DisplayName = "SQL OTP Test", Otp = "123456",
                    ExpiresAt = now.AddMinutes(10), LastSentAt = now
                };
                lockDb.RegistrationOtps.Add(otp);
                await lockDb.SaveChangesAsync();

                for (var attempt = 1; attempt <= RegistrationOtp.MaximumVerificationAttempts; attempt++)
                {
                    var tracked = await lockDb.RegistrationOtps.SingleAsync(x => x.Email == email);
                    var disposition = OtpVerificationPolicy.RecordAttempt(tracked, matches: false, now);
                    await lockDb.SaveChangesAsync();
                    Check(disposition == (attempt == RegistrationOtp.MaximumVerificationAttempts
                            ? OtpAttemptDisposition.Locked : OtpAttemptDisposition.Invalid),
                        $"SQL Server persists OTP failed attempt {attempt} and expected lockout state");
                }

                var locked = await lockDb.RegistrationOtps.SingleAsync(x => x.Email == email);
                Check(locked.VerificationAttempts == RegistrationOtp.MaximumVerificationAttempts &&
                      locked.LockedUntil == locked.ExpiresAt,
                    "SQL Server persists the attempt ceiling and lock expiry");
                Check(OtpVerificationPolicy.RecordAttempt(locked, matches: true, now) == OtpAttemptDisposition.Locked,
                    "A correct OTP cannot bypass a persisted SQL Server lock");
            }

            // Two independent SQL contexts read one version and attempt an update. The rowversion
            // concurrency token must reject the stale second update instead of losing an attempt.
            var concurrentEmail = $"race-{Guid.NewGuid():N}@example.test";
            var initial = new RegistrationOtp
            {
                Email = concurrentEmail, DisplayName = "SQL Race Test", Otp = "654321",
                ExpiresAt = now.AddMinutes(10), LastSentAt = now
            };
            await using (var seedDb = new SurveyDbContext(options))
            {
                seedDb.RegistrationOtps.Add(initial);
                await seedDb.SaveChangesAsync();
            }

            await using var firstDb = new SurveyDbContext(options);
            await using var secondDb = new SurveyDbContext(options);
            var firstCopy = await firstDb.RegistrationOtps.SingleAsync(x => x.Email == concurrentEmail);
            var secondCopy = await secondDb.RegistrationOtps.SingleAsync(x => x.Email == concurrentEmail);
            OtpVerificationPolicy.RecordAttempt(firstCopy, matches: false, now);
            OtpVerificationPolicy.RecordAttempt(secondCopy, matches: false, now);
            await firstDb.SaveChangesAsync();
            var rejected = false;
            try { await secondDb.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { rejected = true; }
            Check(rejected, "SQL Server rowversion rejects a stale concurrent OTP attempt update");

            Console.WriteLine("PASS: OTP SQL integration checks completed against a new local test database.");
        }
        finally
        {
            if (created)
            {
                await using var master = new SqlConnection(localMasterConnection);
                await master.OpenAsync();
                await using var cleanup = master.CreateCommand();
                cleanup.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
                await cleanup.ExecuteNonQueryAsync();
                Console.WriteLine("PASS: Isolated OTP test database was removed after the checks.");
            }
        }
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException($"FAIL: {message}");
        Console.WriteLine("PASS: " + message);
    }
}
