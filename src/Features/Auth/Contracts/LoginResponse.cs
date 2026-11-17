namespace Atlas.Features.Auth.Contracts;

public class LoginResponse
{
    public string? Username { get; set; }

    public DateTime? ExpiresAt { get; set; }
}