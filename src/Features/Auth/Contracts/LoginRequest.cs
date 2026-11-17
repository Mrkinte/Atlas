namespace Atlas.Features.Auth.Contracts;

public sealed record LoginRequest(
    string Username,
    string Password,
    bool RememberMe = false);