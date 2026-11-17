using System.Text;

namespace Atlas.Features.Auth;

/// <summary>
///     上限按 UTF-8 字节数计：BCrypt 只处理前 72 字节，超出部分会被静默截断，
///     与其截断不如直接拒绝
/// </summary>
public static class PasswordPolicy
{
    // 仅在设置新密码时生效；旧密码只做非空校验，避免存量短密码无法登录
    private const int MinLength = 8;

    private const int MaxBytes = 72;

    public static bool IsValid(string? password)
    {
        return password is not null
               && password.Length >= MinLength
               && Encoding.UTF8.GetByteCount(password) <= MaxBytes;
    }
}