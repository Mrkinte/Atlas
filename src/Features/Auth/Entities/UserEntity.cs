using FreeSql.DataAnnotations;

namespace Atlas.Features.Auth.Entities;

[Table(Name = "Users")]
public class UserEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(StringLength = 32, IsNullable = false)]
    public string Username { get; set; } = string.Empty;

    [Column(StringLength = 200, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>修改密码时 +1，该用户已签发的 Token 全部失效</summary>
    [Column(IsNullable = false)]
    public int PasswordVersion { get; set; }
}