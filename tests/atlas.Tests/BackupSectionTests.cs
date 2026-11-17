using System.IO.Compression;
using System.Text.Json.Nodes;
using Atlas.Extensions;
using Atlas.Features.Backup;
using Atlas.Features.Card.Entities;
using Atlas.Features.Image;
using Atlas.Features.Image.Entities;
using Atlas.Features.Live2D.Entities;
using Atlas.Features.Settings;
using Atlas.Features.Settings.Contracts;
using Atlas.Tests.TestSupport;

namespace Atlas.Tests;

/// <summary>
///     分段备份与恢复：只覆盖选中的内容，其余数据与文件必须保持原状
/// </summary>
public class BackupSectionTests
{
    private const string ModelPath = "/uploads/live2d/hiyori/hiyori.model3.json";

    [Fact]
    public async Task 部分导出只写入选中分段与对应文件()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        WriteUploadFile(host, Path.Combine("images", "photo.png"), [1, 2, 3]);
        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "hiyori.model3.json"), [4, 5, 6]);
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "card-1", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        host.Fsql.Insert(new ImageEntity
        {
            Name = "photo.png", RelativePath = "uploads/images/photo.png",
            OriginalName = "photo.png", Kind = ImageService.KindImage
        }).ExecuteAffrows();
        host.Fsql.Insert(new Live2DModelConfigEntity
        {
            ModelPath = ModelPath, UpdatedAt = DateTime.UtcNow
        }).ExecuteAffrows();

        var archive = await ExportAsync(host, BackupSection.Live2D);

        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        await using var manifestStream = zip.GetEntry(TestZip.ManifestEntryName)!.Open();
        var manifest = JsonNode.Parse(manifestStream)!;

        Assert.Equal(["live2d"], manifest["sections"]!.AsArray().Select(s => s!.GetValue<string>()));
        Assert.Null(manifest["settings"]);
        Assert.Null(manifest["cards"]);
        Assert.Null(manifest["images"]);
        Assert.Single(manifest["live2D"]!["configs"]!.AsArray());

        Assert.Equal(
            ["manifest.json", "uploads/live2d/hiyori/hiyori.model3.json"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task 只恢复Live2D时卡片图库与站点设置保持原状()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        WriteUploadFile(host, Path.Combine("images", "keep.png"), [1, 2, 3]);
        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "hiyori.model3.json"), [4, 5, 6]);
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "keep", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        host.Fsql.Insert(new ImageEntity
        {
            Name = "keep.png", RelativePath = "uploads/images/keep.png",
            OriginalName = "keep.png", Kind = ImageService.KindImage
        }).ExecuteAffrows();
        host.Fsql.Insert(new Live2DModelConfigEntity
        {
            ModelPath = ModelPath, UpdatedAt = DateTime.UtcNow
        }).ExecuteAffrows();
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "原配置", 12, 1028, true, null, 0, 0, "#111111", [],
            true, null, null, false, null, true, true, ModelPath));

        var archive = await ExportAsync(host, BackupSection.All);

        // 破坏除 Live2D 之外的现场，恢复后这些改动都应保留
        host.Fsql.Delete<CardEntity>().Where(c => c.Id > 0).ExecuteAffrows();
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "intruder", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        host.Fsql.Delete<ImageEntity>().Where(i => i.Id > 0).ExecuteAffrows();
        host.Fsql.Insert(new ImageEntity
        {
            Name = "other.png", RelativePath = "uploads/images/other.png",
            OriginalName = "other.png", Kind = ImageService.KindImage
        }).ExecuteAffrows();
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "被保留", 18, 2048, false, null, 0, 0, "#000000", [],
            false, null, null, true, null, false, true, ModelPath));
        host.Fsql.Delete<Live2DModelConfigEntity>()
            .Where(c => c.ModelPath != string.Empty).ExecuteAffrows();
        Directory.Delete(Path.Combine(host.Paths.UploadsDirectory, "live2d"), true);
        WriteUploadFile(host, Path.Combine("live2d", "rogue", "rogue.model3.json"), [7, 8, 9]);

        var response = await host.Backup.RestoreAsync(
            TestZip.ToFormFile(archive), BackupSection.Live2D);

        Assert.Equal(0, response.Cards);
        Assert.Equal(0, response.Images);
        Assert.Equal(1, response.Live2DConfigs);
        Assert.Equal(1, response.Files);
        Assert.False(response.Settings);

        Assert.Equal("intruder", host.Fsql.Select<CardEntity>().First().Uuid);
        Assert.Equal("uploads/images/other.png",
            host.Fsql.Select<ImageEntity>().First().RelativePath);
        Assert.True(File.Exists(Path.Combine(host.Paths.UploadsDirectory, "images", "keep.png")));

        var settings = await host.Settings.GetAsync();
        Assert.Equal("被保留", settings.SiteTitle);
        Assert.True(settings.Live2DEnabled);
        Assert.Equal(ModelPath, settings.Live2DModelPath);

        Assert.Equal(
            ["live2d/hiyori/hiyori.model3.json"],
            ListUploadFiles(host).Where(path => path.StartsWith("live2d/")).ToArray());
    }

    [Fact]
    public async Task 只恢复卡片时设置图库与Live2D都保持原状()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        WriteUploadFile(host, Path.Combine("images", "keep.png"), [1, 2, 3]);
        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "hiyori.model3.json"), [4, 5, 6]);
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "keep", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        host.Fsql.Insert(new ImageEntity
        {
            Name = "keep.png", RelativePath = "uploads/images/keep.png",
            OriginalName = "keep.png", Kind = ImageService.KindImage
        }).ExecuteAffrows();
        host.Fsql.Insert(new Live2DModelConfigEntity
        {
            ModelPath = ModelPath, UpdatedAt = DateTime.UtcNow
        }).ExecuteAffrows();
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "原配置", 12, 1028, true, null, 0, 0, "#111111", [],
            true, null, null, false, null, true, true, ModelPath));

        var archive = await ExportAsync(host, BackupSection.All);

        host.Fsql.Delete<CardEntity>().Where(c => c.Id > 0).ExecuteAffrows();
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "intruder", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        host.Fsql.Delete<ImageEntity>().Where(i => i.Id > 0).ExecuteAffrows();
        WriteUploadFile(host, Path.Combine("images", "rogue.png"), [9]);
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "被保留", 18, 2048, false, null, 0, 0, "#000000", [],
            false, null, null, true, null, false, false, null));
        host.Fsql.Delete<Live2DModelConfigEntity>()
            .Where(c => c.ModelPath != string.Empty).ExecuteAffrows();

        var response = await host.Backup.RestoreAsync(
            TestZip.ToFormFile(archive), BackupSection.Cards);

        Assert.Equal(1, response.Cards);
        Assert.Equal(0, response.Images);
        Assert.Equal(0, response.Live2DConfigs);
        Assert.Equal(0, response.Files);

        Assert.Equal("keep", host.Fsql.Select<CardEntity>().First().Uuid);
        var settings = await host.Settings.GetAsync();
        Assert.Equal("被保留", settings.SiteTitle);
        Assert.False(settings.Live2DEnabled);
        Assert.Equal(string.Empty, settings.Live2DModelPath);
        Assert.Empty(host.Fsql.Select<ImageEntity>().ToList());
        Assert.Empty(host.Fsql.Select<Live2DModelConfigEntity>()
            .Where(c => c.ModelPath != string.Empty).ToList());
        Assert.True(File.Exists(Path.Combine(host.Paths.UploadsDirectory, "images", "rogue.png")));
        Assert.True(File.Exists(Path.Combine(host.Paths.UploadsDirectory, "live2d", "hiyori",
            "hiyori.model3.json")));
    }

    [Fact]
    public async Task 只恢复设置时保留当前Live2D开关与模型()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "hiyori.model3.json"), [1, 2, 3]);
        host.Fsql.Insert(new Live2DModelConfigEntity
        {
            ModelPath = ModelPath, UpdatedAt = DateTime.UtcNow
        }).ExecuteAffrows();
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "原配置", 12, 1028, true, null, 0, 0, "#111111", [],
            true, null, null, false, null, true, true, ModelPath));

        var archive = await ExportAsync(host, BackupSection.All);

        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "当前配置", 18, 2048, false, null, 0, 0, "#000000", [],
            false, null, null, true, null, false, false, null));

        var response = await host.Backup.RestoreAsync(
            TestZip.ToFormFile(archive), BackupSection.Settings);

        Assert.True(response.Settings);
        Assert.Equal(0, response.Cards);
        Assert.Equal(0, response.Files);

        var settings = await host.Settings.GetAsync();
        Assert.Equal("原配置", settings.SiteTitle);
        // Live2D 段未选中，开关与模型保持恢复前的值
        Assert.False(settings.Live2DEnabled);
        Assert.Equal(string.Empty, settings.Live2DModelPath);
        Assert.Single(host.Fsql.Select<Live2DModelConfigEntity>()
            .Where(c => c.ModelPath != string.Empty).ToList());
        Assert.True(File.Exists(Path.Combine(host.Paths.UploadsDirectory, "live2d", "hiyori",
            "hiyori.model3.json")));
    }

    [Fact]
    public async Task 检查接口返回包内可恢复的分段与数量()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        WriteUploadFile(host, Path.Combine("images", "photo.png"), [1]);
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "card-1", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        host.Fsql.Insert(new ImageEntity
        {
            Name = "photo.png", RelativePath = "uploads/images/photo.png",
            OriginalName = "photo.png", Kind = ImageService.KindImage
        }).ExecuteAffrows();

        var archive = await ExportAsync(
            host, BackupSection.Cards | BackupSection.Images);
        var inspect = await host.Backup.InspectAsync(TestZip.ToFormFile(archive));

        Assert.Equal(["cards", "images"], inspect.Sections);
        Assert.Equal(1, inspect.Cards);
        Assert.Equal(1, inspect.Images);
        Assert.Equal(0, inspect.Live2DConfigs);

        // v1 包没有 sections 字段，按全部内容处理
        var legacy = await host.Backup.InspectAsync(
            TestZip.ToFormFile(TestZip.EmptyArchive()));
        Assert.Equal(["cards", "settings", "images", "live2d"], legacy.Sections);
        Assert.Equal(BackupLimits.CurrentVersion, legacy.Version);
    }

    [Fact]
    public async Task 恢复未包含在备份中的分段被拒绝且不改动数据()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "keep", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();

        var archive = await ExportAsync(host, BackupSection.Cards);

        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(
                TestZip.ToFormFile(archive), BackupSection.Live2D));
        Assert.Contains("备份中不包含", error.Message);
        Assert.Equal("keep", host.Fsql.Select<CardEntity>().First().Uuid);
    }

    private static async Task<byte[]> ExportAsync(
        TestHost host, BackupSection sections)
    {
        await using var export = await host.Backup.CreateExportArchiveAsync(sections);
        using var copy = new MemoryStream();
        await export.CopyToAsync(copy);
        return copy.ToArray();
    }

    private static void WriteUploadFile(
        TestHost host, string relativePath, byte[] content)
    {
        var fullPath = Path.Combine(host.Paths.UploadsDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
    }

    private static string[] ListUploadFiles(TestHost host)
    {
        return Directory.EnumerateFiles(host.Paths.UploadsDirectory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(host.Paths.UploadsDirectory, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
