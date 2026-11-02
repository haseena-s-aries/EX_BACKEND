using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

internal static class OtpSecurityChecks
{
    public static async Task Run()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new RegistrationOtp
        {
            Email = "survey@example.test",
            DisplayName = "Surveyor",
            Otp = "1234",
            ExpiresAt = now.AddMinutes(10),
            LastSentAt = now
        };

        for (var attempt = 1; attempt < RegistrationOtp.MaximumVerificationAttempts; attempt++)
            Check(OtpVerificationPolicy.RecordAttempt(otp, matches: false, now) == OtpAttemptDisposition.Invalid,
                $"Invalid OTP attempt {attempt} is counted and rejected");

        Check(OtpVerificationPolicy.RecordAttempt(otp, matches: false, now) == OtpAttemptDisposition.Locked,
            "Maximum invalid OTP attempts lock the code");
        Check(otp.VerificationAttempts == RegistrationOtp.MaximumVerificationAttempts && otp.LockedUntil == otp.ExpiresAt,
            "Lock persists until the current OTP expires");
        Check(otp.ConcurrencyVersion == RegistrationOtp.MaximumVerificationAttempts,
            "OTP attempt updates advance the concurrency token");
        Check(OtpVerificationPolicy.RecordAttempt(otp, matches: true, now) == OtpAttemptDisposition.Locked,
            "Correct OTP cannot bypass an active lock");

        OtpVerificationPolicy.Reset(otp);
        otp.Otp = "5678";
        otp.ExpiresAt = now.AddMinutes(10);
        Check(otp.VerificationAttempts == 0 && otp.LockedUntil is null,
            "Issuing a fresh OTP resets attempt count and lock");
        Check(otp.ConcurrencyVersion == RegistrationOtp.MaximumVerificationAttempts + 1,
            "Issuing a fresh OTP invalidates stale concurrent verifications");
        Check(OtpVerificationPolicy.RecordAttempt(otp, matches: true, now) == OtpAttemptDisposition.Accepted,
            "Correct OTP is accepted before the attempt limit");

        var databaseName = Guid.NewGuid().ToString();
        var firstOptions = new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(databaseName).Options;
        var secondOptions = new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(databaseName).Options;
        await using var firstDb = new SurveyDbContext(firstOptions);
        await using var secondDb = new SurveyDbContext(secondOptions);
        var persistedOtp = new RegistrationOtp
        {
            Email = "concurrent@example.test", DisplayName = "Surveyor", Otp = "2468",
            ExpiresAt = now.AddMinutes(10), LastSentAt = now
        };
        firstDb.RegistrationOtps.Add(persistedOtp);
        await firstDb.SaveChangesAsync();
        var firstCopy = await firstDb.RegistrationOtps.SingleAsync();
        var secondCopy = await secondDb.RegistrationOtps.SingleAsync();
        OtpVerificationPolicy.RecordAttempt(firstCopy, matches: false, now);
        OtpVerificationPolicy.RecordAttempt(secondCopy, matches: false, now);
        await firstDb.SaveChangesAsync();
        var staleWriteRejected = false;
        try
        {
            await secondDb.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            staleWriteRejected = true;
        }
        Check(staleWriteRejected, "Concurrent OTP verification cannot overwrite a newer attempt count");

        foreach (var action in new[] { "SendRegistrationOtp", "VerifyRegistrationOtp", "ResendRegistrationOtp", "CompleteRegistration", "Login" })
        {
            var method = typeof(AuthController).GetMethod(action)!;
            var limiter = method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
                .Cast<EnableRateLimitingAttribute>().SingleOrDefault();
            Check(limiter?.PolicyName == "AnonymousAuth", $"{action} is protected by the anonymous-auth IP throttle");
        }

        Console.WriteLine("OTP security checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
        Console.WriteLine($"PASS: {message}");
    }
}
