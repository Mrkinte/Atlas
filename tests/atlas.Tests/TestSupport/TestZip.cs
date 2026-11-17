using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Atlas.Tests.TestSupport;

/// <summary>
///     在内存里构造备份 ZIP，正常包与恶意包都从这里生成，无磁盘残留与时序依赖
/// </summary>
internal static class TestZip
{
    /// <summary>BackupManifestBuilder 中的同名常量是 private，这里手写以驱动公开恢复入口</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>settings 只填非空校验必需的 siteTitle，其余字段走契约默认值</summary>
    public const string MinimalManifest =
        """{"version":1,"appVersion":"tests","exportedAt":"2026-01-01T00:00:00Z","settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],"images":[]}""";

    public static byte[] Utf8(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    /// <summary>只含合法空清单的备份包</summary>
    public static byte[] EmptyArchive()
    {
        return CreateArchive((ManifestEntryName, Utf8(MinimalManifest)));
    }

    /// <summary>条目名允许包含 ../、反斜杠等恶意形态，ZIP 格式本身不校验</summary>
    public static byte[] CreateArchive(params (string EntryName, byte[] Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }
        }

        return output.ToArray();
    }

    /// <summary>包装成恢复入口接受的 IFormFile，模拟浏览器上传</summary>
    public static FormFile ToFormFile(byte[] bytes, string fileName = "backup.zip")
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }
}