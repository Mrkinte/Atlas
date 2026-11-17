using Atlas.Extensions;

namespace Atlas.Features.Backup;

/// <summary>备份内容分段，导出与恢复都按这里的粒度处理</summary>
[Flags]
public enum BackupSection
{
    None = 0,
    Cards = 1 << 0,
    Settings = 1 << 1,
    Images = 1 << 2,
    Live2D = 1 << 3,
    All = Cards | Settings | Images | Live2D
}

/// <summary>分段在清单与接口参数中的名称</summary>
internal static class BackupSectionNames
{
    internal const string Cards = "cards";
    internal const string Settings = "settings";
    internal const string Images = "images";
    internal const string Live2D = "live2d";

    internal static readonly string[] All = [Cards, Settings, Images, Live2D];

    /// <summary>参数缺省表示全选，兼容不带分段参数的旧调用</summary>
    internal static BackupSection Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return BackupSection.All;

        var sections = BackupSection.None;
        foreach (var name in value.Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            sections |= name.ToLowerInvariant() switch
            {
                Cards => BackupSection.Cards,
                Settings => BackupSection.Settings,
                Images => BackupSection.Images,
                Live2D => BackupSection.Live2D,
                _ => throw new InvalidRequestException($"未知的备份内容：{name}")
            };

        if (sections == BackupSection.None)
            throw new InvalidRequestException("请至少选择一项备份内容");

        return sections;
    }

    internal static BackupSection FromNames(IEnumerable<string>? names)
    {
        return names is null ? BackupSection.All : Parse(string.Join(',', names));
    }

    internal static string[] ToNames(BackupSection sections)
    {
        var names = new List<string>(4);
        if (sections.HasFlag(BackupSection.Cards)) names.Add(Cards);
        if (sections.HasFlag(BackupSection.Settings)) names.Add(Settings);
        if (sections.HasFlag(BackupSection.Images)) names.Add(Images);
        if (sections.HasFlag(BackupSection.Live2D)) names.Add(Live2D);
        return [.. names];
    }

    internal static string Describe(BackupSection sections)
    {
        var names = new List<string>(4);
        if (sections.HasFlag(BackupSection.Cards)) names.Add("卡片");
        if (sections.HasFlag(BackupSection.Settings)) names.Add("站点设置");
        if (sections.HasFlag(BackupSection.Images)) names.Add("图库");
        if (sections.HasFlag(BackupSection.Live2D)) names.Add("Live2D");
        return string.Join("、", names);
    }
}