namespace Atlas.Features.Auth.Contracts;

public class AuthStatusResponse
{
    public bool IsAuthenticated { get; set; }

    public string? Username { get; set; }
}