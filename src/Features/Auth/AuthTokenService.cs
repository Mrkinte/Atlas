using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Features.Auth;

public class AuthTokenService(JwtKeyProvider keyProvider)
{
    public (string Token, DateTime ExpiresAt) GenerateToken(
        int userId, string username, int passwordVersion)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyProvider.GetKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(JwtDefaults.AccessTokenExpiresMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtDefaults.PasswordVersionClaim, passwordVersion.ToString()),
            // jti 用于登出时精确吊销单个 Token
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            JwtDefaults.Issuer,
            JwtDefaults.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}