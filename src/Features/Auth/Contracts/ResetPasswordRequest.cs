namespace Atlas.Features.Auth.Contracts;

public sealed record ResetPasswordRequest(
    string OldPassword,
    string NewPassword);