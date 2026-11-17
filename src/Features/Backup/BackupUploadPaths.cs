using System.IO.Compression;
using Atlas.Extensions;

namespace Atlas.Features.Backup;

/// <summary>
///     备份包内上传文件条目的路径约定与安全解析：图片固定为 uploads/images|icons/&lt;文件名&gt;，
///     模型保留 uploads/live2d/&lt;模型目录&gt;/... 完整结构。导出与恢复共用同一套规则
/// </summary>
internal static class BackupUploadPaths
{
    internal const string UploadsPrefix = "uploads/";

    // 模型由多个文件与子目录组成，需要递归打包
    internal const string Live2DFolder = "live2d";

    internal static readonly string[] UploadFolders = ["images", "icons"];

    internal static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico"
        };

    /// <summary>模型目录内允许恢复的扩展名，避免把脚本写进可访问的上传目录</summary>
    internal static readonly HashSet<string> Live2DAllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".moc", ".moc3", ".mtn", ".png", ".jpg", ".jpeg", ".webp",
            ".gif", ".bmp", ".svg", ".ico", ".mp3", ".wav", ".ogg", ".m4a"
        };

    internal static bool TryResolveUploadEntry(
        string path, out string folder, out string fileName)
    {
        folder = string.Empty;
        fileName = string.Empty;

        var normalized = ZipArchiveSafety.NormalizeRelativePath(path);
        var segments = normalized.Split('/');
        if (segments.Length != 3
            || segments[0] != "uploads"
            || segments[1] is not ("images" or "icons"))
            return false;

        var name = segments[2];
        if (!ZipArchiveSafety.IsSafeFileName(name)) return false;
        if (!AllowedExtensions.Contains(Path.GetExtension(name))) return false;

        folder = segments[1];
        fileName = name;
        return true;
    }

    /// <summary>逐段校验路径并限制扩展名，模型目录与文件段允许中文等 Unicode 名称</summary>
    internal static bool TryResolveLive2DEntry(string path, out string entryPath)
    {
        entryPath = string.Empty;

        var normalized = ZipArchiveSafety.NormalizeRelativePath(path);
        var segments = normalized.Split('/');
        if (segments.Length < 4
            || segments[0] != "uploads"
            || segments[1] != Live2DFolder)
            return false;

        // 手工放入的模型也要能备份，放宽到 Unicode 名称
        if (!ZipArchiveSafety.IsSafeLive2DSegment(segments[2])) return false;
        for (var i = 3; i < segments.Length; i++)
            if (!ZipArchiveSafety.IsSafeLive2DSegment(segments[i]))
                return false;

        if (!Live2DAllowedExtensions.Contains(Path.GetExtension(segments[^1]))) return false;

        entryPath = string.Join('/', segments);
        return true;
    }

    internal static string? ToLive2DEntryPath(string modelPath)
    {
        if (!modelPath.StartsWith(
                "/uploads/live2d/", StringComparison.OrdinalIgnoreCase))
            return null;

        return TryResolveLive2DEntry(modelPath[1..], out var entryPath)
            ? entryPath
            : null;
    }

    /// <summary>包内缺少对应文件时回退内置模型</summary>
    internal static string? ResolveRestoredModelPath(
        string? modelPath,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        if (modelPath is null) return null;
        if (modelPath.StartsWith("/live2d/", StringComparison.OrdinalIgnoreCase)) return modelPath;

        var entryPath = ToLive2DEntryPath(modelPath);
        return entryPath is not null && entries.ContainsKey(entryPath)
            ? modelPath
            : null;
    }

    /// <summary>上传模型需包内存在对应文件，否则恢复后会指向不存在的路径</summary>
    internal static bool IsLive2DConfigRestorable(
        string modelPath,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        if (!modelPath.StartsWith(
                "/uploads/live2d/", StringComparison.OrdinalIgnoreCase))
            return true;

        var entryPath = ToLive2DEntryPath(modelPath);
        return entryPath is not null && entries.ContainsKey(entryPath);
    }
}