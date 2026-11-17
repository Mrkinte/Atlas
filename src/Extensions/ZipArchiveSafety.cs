namespace Atlas.Extensions;

/// <summary>
///     ZIP 条目路径与文件名的安全校验，备份恢复与 Live2D 模型上传共用，只做纯判断、不碰文件系统
///     路径先经 <see cref="NormalizeRelativePath" /> 规范化为正斜杠再逐段校验，避免 "..\" 绕过检查
/// </summary>
internal static class ZipArchiveSafety
{
    internal static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    /// <summary>
    ///     只允许字母、数字、点、下划线与连字符。文件名会直接拼进可公开访问的上传目录，
    ///     因此用最严格的 ASCII 白名单，杜绝 Unicode 混淆与保留字符
    /// </summary>
    internal static bool IsSafeFileName(string name)
    {
        if (name.Length is 0 or > 128) return false;
        if (name != Path.GetFileName(name)) return false;

        // 拒绝当前目录与上级目录，避免嵌套路径借 ".." 逃出上传目录
        if (name is "." or "..") return false;

        foreach (var c in name)
            if (!char.IsAsciiLetterOrDigit(c)
                && c != '.'
                && c != '_'
                && c != '-')
                return false;

        return true;
    }

    /// <summary>
    ///     模型文件的段名只拦穿越段、控制字符与 Windows 非法字符
    ///     模型目录名由用户上传的压缩包决定，按 ASCII 白名单会丢掉合法的中文模型
    /// </summary>
    internal static bool IsSafeLive2DSegment(string segment)
    {
        if (segment.Length is 0 or > 200) return false;
        if (segment is "." or "..") return false;
        if (segment != Path.GetFileName(segment)) return false;
        if (segment.EndsWith(' ') || segment.EndsWith('.')) return false;

        foreach (var c in segment)
            if (char.IsControl(c) || c is '<' or '>' or ':' or '"' or '|' or '?'
                    or '*')
                return false;

        return true;
    }

    /// <summary>
    ///     只接受不含根路径与 .. 段的相对路径，用于 Live2D 模型解压
    ///     落盘目标已固定在上传目录内，非法文件名由文件系统写入失败兜底
    /// </summary>
    internal static bool IsSafeRelativePath(string path)
    {
        if (path.Length is 0 or > 1024 || Path.IsPathRooted(path)) return false;

        return NormalizeRelativePath(path).Split('/').All(segment => segment != "..");
    }
}