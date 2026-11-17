namespace Atlas.Features.Backup;

/// <summary>ZIP 读取需要可定位的流，上传内容先落到临时文件</summary>
internal static class BackupTempArchive
{
    internal static async Task<string> SaveAsync(
        IFormFile file, string tag, CancellationToken ct)
    {
        var path = Path.Combine(
            Path.GetTempPath(), $"{tag}-{Guid.NewGuid():N}.zip");

        try
        {
            await using var target = File.Create(path);
            await file.CopyToAsync(target, ct);
            return path;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
}