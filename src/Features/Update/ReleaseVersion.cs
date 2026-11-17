using System.Globalization;

namespace Atlas.Features.Update;

/// <summary>
///     只比较版本号前缀的数字段，忽略 v 前缀与 -dev、+hash 后缀。
///     tag 与程序集版本写法不一致时，字符串比较会得出错误结论
/// </summary>
internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch)
    : IComparable<ReleaseVersion>
{
    /// <summary>允许 1 到 4 段数字，第 4 段（revision）不参与比较</summary>
    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var core = value.Trim();
        if (core[0] is 'v' or 'V') core = core[1..];
        if (core.Length == 0) return false;

        var cut = core.IndexOfAny(['-', '+']);
        if (cut >= 0) core = core[..cut];

        var parts = core.Split('.');
        if (parts.Length is < 1 or > 4) return false;

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return false;

            if (i < numbers.Length) numbers[i] = number;
        }

        version = new ReleaseVersion(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }
}
