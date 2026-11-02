using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Services;

public enum OtpAttemptDisposition
{
    Accepted,
    Invalid,
    Locked
}

public static class OtpVerificationPolicy
{
    public static OtpAttemptDisposition RecordAttempt(
        RegistrationOtp registration, bool matches, DateTimeOffset now)
    {
        if (registration.LockedUntil > now ||
            registration.VerificationAttempts >= RegistrationOtp.MaximumVerificationAttempts)
            return OtpAttemptDisposition.Locked;

        registration.ConcurrencyVersion++;
        registration.VerificationAttempts++;
        if (matches)
            return OtpAttemptDisposition.Accepted;

        if (registration.VerificationAttempts >= RegistrationOtp.MaximumVerificationAttempts)
        {
            registration.LockedUntil = registration.ExpiresAt;
            return OtpAttemptDisposition.Locked;
        }

        return OtpAttemptDisposition.Invalid;
    }

    public static void Reset(RegistrationOtp registration)
    {
        registration.VerificationAttempts = 0;
        registration.LockedUntil = null;
        registration.ConcurrencyVersion++;
    }
}
