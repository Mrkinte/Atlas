namespace Atlas.Features.Auth.Contracts;

public sealed record ChangeUsernameRequest(
    string NewUsername,
    string Password);