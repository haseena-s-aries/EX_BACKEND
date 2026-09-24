using System.ComponentModel.DataAnnotations;

namespace Ex_SurveyApp_api.Models;

public record SendRegistrationOtpRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required] UserRole? Role);

public record VerifyRegistrationOtpRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, RegularExpression("^[0-9]{4}$", ErrorMessage = "OTP must contain exactly 4 digits.")] string Otp);

public record ResendRegistrationOtpRequest(
    [Required, EmailAddress, StringLength(320)] string Email);

public record CompleteRegistrationRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record CreateUserRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(200)] string DisplayName,
    [Required] string Password,
    [Required] UserRole Role);

public record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    ApprovalStatus ApprovalStatus);

public record PendingApprovalResponse(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    ApprovalStatus ApprovalStatus);

public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
