using Atlas.Extensions;
using Atlas.Features.Settings.Contracts;
using Atlas.Tests.TestSupport;

namespace Atlas.Tests;

/// <summary>
///     ZIP 安全回归保护：通过公开的 RestoreAsync 入口喂恶意 ZIP，
///     断言恢复被拒绝或忽略，且绝不把内容写到上传目录之外
/// </summary>
public class BackupZipSafetyTests
{
    /// <summary>
    ///     收集数据目录下全部文件的相对路径快照
    ///     恶意条目若逃过校验必然多出文件，前后一致即证明没有写出任何东西
    /// </summary>
    private static string[] SnapshotDataDirectory(TestHost host)
    {
        return Directory.EnumerateFiles(host.DataDirectory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(host.DataDirectory, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public async Task 相对路径穿越条目被忽略且不写出文件()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var before = SnapshotDataDirectory(host);

        // ../ 逃出 uploads/images 后落点分别是数据目录根与 uploads 目录
        var zip = TestZip.CreateArchive(
            (TestZip.ManifestEntryName, TestZip.Utf8(TestZip.MinimalManifest)),
            ("uploads/images/../../evil.png", TestZip.Utf8("evil")),
            ("uploads/images/sub/../../../evil2.png", TestZip.Utf8("evil")));

        var response = await host.Backup.RestoreAsync(TestZip.ToFormFile(zip));

        Assert.Equal(0, response.Files);
        Assert.False(File.Exists(Path.Combine(host.DataDirectory, "evil.png")),
            "穿越条目不得写到 uploads 之外");
        Assert.False(File.Exists(Path.Combine(host.DataDirectory, "evil2.png")));
        Assert.Empty(Directory.GetFiles(host.Paths.UploadsDirectory, "evil*.png", SearchOption.AllDirectories));
        Assert.Equal(before, SnapshotDataDirectory(host));
    }

    [Fact]
    public async Task 反斜杠与绝对路径条目被忽略且不写出文件()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var before = SnapshotDataDirectory(host);

        var zip = TestZip.CreateArchive(
            (TestZip.ManifestEntryName, TestZip.Utf8(TestZip.MinimalManifest)),
            // 反斜杠会被 NormalizeRelativePath 转成正斜杠，仍无法逃逸
            ("uploads\\..\\evil.png", TestZip.Utf8("evil")),
            // 绝对路径：POSIX 根与 Windows 盘符
            ("/uploads/images/evil.png", TestZip.Utf8("evil")),
            ("C:/evil/evil.png", TestZip.Utf8("evil")));

        var response = await host.Backup.RestoreAsync(TestZip.ToFormFile(zip));

        Assert.Equal(0, response.Files);
        Assert.Empty(Directory.GetFiles(host.Paths.UploadsDirectory, "evil*", SearchOption.AllDirectories));
        Assert.Equal(before, SnapshotDataDirectory(host));
    }

    [Fact]
    public async Task 非法扩展名条目被忽略且不写出文件()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var before = SnapshotDataDirectory(host);

        var zip = TestZip.CreateArchive(
            (TestZip.ManifestEntryName, TestZip.Utf8(TestZip.MinimalManifest)),
            ("uploads/images/evil.exe", TestZip.Utf8("evil")),
            ("uploads/live2d/model/run.exe", TestZip.Utf8("evil")));

        var response = await host.Backup.RestoreAsync(TestZip.ToFormFile(zip));

        Assert.Equal(0, response.Files);
        Assert.Empty(Directory.GetFiles(host.Paths.UploadsDirectory, "evil*", SearchOption.AllDirectories));
        Assert.Equal(before, SnapshotDataDirectory(host));
    }

    [Fact]
    public async Task Live2D目录穿越与非法段名条目被忽略且不写出文件()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var before = SnapshotDataDirectory(host);

        var zip = TestZip.CreateArchive(
            (TestZip.ManifestEntryName, TestZip.Utf8(TestZip.MinimalManifest)),
            ("uploads/live2d/../evil.json", TestZip.Utf8("evil")),
            // ".." 深层嵌套同样要被逐段校验拦下
            ("uploads/live2d/ok/../../evil2.json", TestZip.Utf8("evil")),
            // Windows 保留字符出现在模型目录段
            ("uploads/live2d/a<b/pose.json", TestZip.Utf8("evil")),
            // 当前目录段 "." 不合法
            ("uploads/live2d/./m.json", TestZip.Utf8("evil")));

        var response = await host.Backup.RestoreAsync(TestZip.ToFormFile(zip));

        Assert.Equal(0, response.Files);
        Assert.Empty(Directory.GetFiles(host.Paths.UploadsDirectory, "evil*", SearchOption.AllDirectories));
        Assert.Equal(before, SnapshotDataDirectory(host));
    }

    [Fact]
    public async Task 超过单文件大小上限的条目被拒绝且不改动现有数据()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "原配置", 12, 1028,
            true, null, 0,
            0, "#111111", [],
            true, null, null, false,
            null, false, false,
            null));
        var before = SnapshotDataDirectory(host);

        // 50 MB 上限 + 1 字节全零内容（压缩后很小，可快速构造）
        var oversized = new byte[50L * 1024 * 1024 + 1];
        var zip = TestZip.CreateArchive(
            (TestZip.ManifestEntryName, TestZip.Utf8(TestZip.MinimalManifest)),
            ("uploads/images/big.png", oversized));

        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile(zip)));
        Assert.Contains("过大", error.Message);

        // 超限在解包前发现，现有数据与文件不受影响
        Assert.False(File.Exists(Path.Combine(host.Paths.UploadsDirectory, "images", "big.png")));
        Assert.Equal("原配置", (await host.Settings.GetAsync()).SiteTitle);
        Assert.Equal(before, SnapshotDataDirectory(host));
    }

    [Fact]
    public async Task 重复条目被拒绝()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        var zip = TestZip.CreateArchive(
            (TestZip.ManifestEntryName, TestZip.Utf8(TestZip.MinimalManifest)),
            ("uploads/images/dup.png", TestZip.Utf8("a")),
            ("uploads/images/dup.png", TestZip.Utf8("b")));

        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile(zip)));
        Assert.Contains("重复文件", error.Message);
    }

    [Fact]
    public async Task 缺少清单或非ZIP文件被拒绝()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        // 只有上传文件、没有 manifest.json
        var noManifest = TestZip.CreateArchive(("uploads/images/a.png", TestZip.Utf8("a")));
        var missing =
            await Assert.ThrowsAsync<InvalidRequestException>(() =>
                host.Backup.RestoreAsync(TestZip.ToFormFile(noManifest)));
        Assert.Contains("缺少 manifest.json", missing.Message);

        // 根本不是 ZIP
        var notZip = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile("this is not a zip"u8.ToArray())));
        Assert.Contains("不是有效的 ZIP", notZip.Message);

        // 空文件
        var empty = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile([])));
        Assert.Contains("为空或超过 512 MB", empty.Message);
    }

    [Fact]
    public async Task 清单版本异常被拒绝()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        // 版本号缺失
        var noVersion = """{"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],"images":[]}""";
        var missing = await Assert.ThrowsAsync<InvalidRequestException>(() => host.Backup.RestoreAsync(
            TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8(noVersion))))));
        Assert.Contains("缺少版本号", missing.Message);

        // 版本号非法
        var badVersion = """{"version":0,"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],"images":[]}""";
        var invalid = await Assert.ThrowsAsync<InvalidRequestException>(() => host.Backup.RestoreAsync(
            TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8(badVersion))))));
        Assert.Contains("版本无效", invalid.Message);

        // 未来版本：旧程序不得恢复新格式
        var futureVersion =
            """{"version":99,"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],"images":[]}""";
        var future = await Assert.ThrowsAsync<InvalidRequestException>(() => host.Backup.RestoreAsync(
            TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8(futureVersion))))));
        Assert.Contains("更新版本的 Atlas", future.Message);
    }

    [Fact]
    public async Task 清单内容非法被拒绝()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        // JSON 语法错误
        var corrupt = await Assert.ThrowsAsync<InvalidRequestException>(() => host.Backup.RestoreAsync(
            TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8("{not json"))))));
        Assert.Contains("无法解析", corrupt.Message);

        // 卡片 uuid 重复
        var duplicateCard = """
                            {"version":1,"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},
                             "cards":[{"uuid":"a"},{"uuid":"a"}],"images":[]}
                            """;
        var dupCard = await Assert.ThrowsAsync<InvalidRequestException>(() => host.Backup.RestoreAsync(
            TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8(duplicateCard))))));
        Assert.Contains("重复卡片", dupCard.Message);

        // 图片类型与目录不匹配（icon 记录却指向 images 目录）
        var kindMismatch = """
                           {"version":1,"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],
                            "images":[{"relativePath":"uploads/images/a.png","originalName":"a.png","kind":"icon","createdAt":"2026-01-01T00:00:00Z"}]}
                           """;
        var mismatch = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8(kindMismatch))))));
        Assert.Contains("图片类型与路径不匹配", mismatch.Message);

        // 图片路径重复
        var duplicateImage = """
                             {"version":1,"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],
                              "images":[{"relativePath":"uploads/images/a.png","originalName":"a.png","kind":"image","createdAt":"2026-01-01T00:00:00Z"},
                                        {"relativePath":"uploads/images/a.png","originalName":"a.png","kind":"image","createdAt":"2026-01-01T00:00:00Z"}]}
                             """;
        var dupImage = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile(TestZip.CreateArchive(
                (TestZip.ManifestEntryName, TestZip.Utf8(duplicateImage))))));
        Assert.Contains("重复图片", dupImage.Message);
    }
}