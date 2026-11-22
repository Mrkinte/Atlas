using System.IO.Compression;
using System.Text.Json;
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
///     备份回归保护：真实 SQLite 与目录结构，验证导出 → 清空 → 恢复后
///     卡片、配置、图库与 Live2D 模型保持一致
/// </summary>
public class BackupRoundTripTests
{
    [Fact]
    public async Task 导出后清空数据再恢复应完整还原卡片配置图库与模型()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        // 站点配置 + 两张卡片 + 图库（图片/图标各一）+ 上传的 Live2D 模型（含子目录）
        var photoBytes = DeterministicBytes(1024, 0x10);
        var iconBytes = DeterministicBytes(256, 0x20);
        var modelJson = DeterministicBytes(512, 0x30);
        var motionJson = DeterministicBytes(384, 0x40);
        var textureBytes = DeterministicBytes(768, 0x50);
        WriteUploadFile(host, Path.Combine("images", "photo.png"), photoBytes);
        WriteUploadFile(host, Path.Combine("icons", "site.ico"), iconBytes);
        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "hiyori.model3.json"), modelJson);
        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "motions", "idle.motion3.json"), motionJson);
        WriteUploadFile(host, Path.Combine("live2d", "hiyori", "hiyori.texture.png"), textureBytes);

        const string modelPath = "/uploads/live2d/hiyori/hiyori.model3.json";
        // 初始化留下的默认分组先清掉，让分组快照只反映本用例的数据
        host.Fsql.Delete<CardGroupEntity>().Where(g => g.Id > 0).ExecuteAffrows();
        host.Fsql.Insert(new CardGroupEntity
        {
            Uuid = "group-1", Name = "常用", SortOrder = 0, Collapsed = false
        }).ExecuteAffrows();
        host.Fsql.Insert(new CardGroupEntity
        {
            Uuid = "group-2", Name = "其他", SortOrder = 1, Collapsed = true
        }).ExecuteAffrows();

        var cards = new[]
        {
            new CardEntity
            {
                Uuid = "card-1", GroupUuid = "group-1",
                SmX = 0, SmY = 0, SmWidth = 2, SmHeight = 2,
                LgX = 1, LgY = 1, LgWidth = 4, LgHeight = 2,
                MinWidth = 120, MinHeight = 90, IsVisible = true, MobileVisible = true,
                Type = "clock",
                Data = """{"tz":"Asia/Shanghai"}"""
            },
            new CardEntity
            {
                Uuid = "card-2", GroupUuid = "group-2",
                SmX = 2, SmY = 0, SmWidth = 1, SmHeight = 1,
                LgX = 5, LgY = 0, LgWidth = 2, LgHeight = 1,
                MinWidth = 60, MinHeight = 60, IsVisible = false, MobileVisible = false,
                Type = "service",
                Data = null
            }
        };
        host.Fsql.Insert(cards).ExecuteAffrows();

        // 创建时间固定为 UTC，保证清单 JSON 往返后的比较是确定性的
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        host.Fsql.Insert(new ImageEntity
        {
            Name = "photo.png", RelativePath = "uploads/images/photo.png",
            OriginalName = "原始名.png", Kind = ImageService.KindImage, CreatedAt = createdAt
        }).ExecuteAffrows();
        host.Fsql.Insert(new ImageEntity
        {
            Name = "site.ico", RelativePath = "uploads/icons/site.ico",
            OriginalName = "site.ico", Kind = ImageService.KindIcon, CreatedAt = createdAt
        }).ExecuteAffrows();

        // 布局值取 NormalizeLayout 会原样保留的稳定区间（scale 0.05~0.5、position 0~1、freeRatio ±100），
        // 避免断言混入"恢复时的钳制归一化"这一独立行为
        host.Fsql.Insert(new Live2DModelConfigEntity
        {
            ModelPath = modelPath, EntryMotionId = "entry-1", IdleMotionId = "idle-1",
            Expression = "smile",
            Outfits = SettingsService.SerializeLive2DOutfits([new Live2DOutfitSelection("hat", "red")]),
            SoundEnabled = true,
            Scale = 0.3, PositionX = 0.4, PositionY = 0.6, FreeRatioX = -0.2, FreeRatioY = 1.3,
            UpdatedAt = DateTime.UtcNow
        }).ExecuteAffrows();

        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "我的主页", 18, 2048,
            true, "https://example.com/bg.jpg",
            8, 30, "#18A058",
            ["cpu0", "gpu1"], true,
            "副标题", "/uploads/icons/site.ico", true,
            "页脚", false, true,
            modelPath));

        // —— 快照导出时刻的全部状态，作为恢复后的对账基准 ——
        var settingsBefore = await host.Settings.GetAsync();
        var groupsBefore = host.Fsql.Select<CardGroupEntity>()
            .OrderBy(g => new { g.SortOrder, g.Id }).ToList();
        var cardsBefore = host.Fsql.Select<CardEntity>().OrderBy(c => c.Uuid).ToList();
        var imagesBefore = host.Fsql.Select<ImageEntity>().OrderBy(i => i.RelativePath).ToList();
        var live2dBefore = host.Fsql.Select<Live2DModelConfigEntity>().OrderBy(c => c.ModelPath).ToList();

        // —— 导出：备份流带 DeleteOnClose 语义，必须先复制到内存再释放 ——
        byte[] archive;
        await using (var export = await host.Backup.CreateExportArchiveAsync())
        {
            using var copy = new MemoryStream();
            await export.CopyToAsync(copy);
            archive = copy.ToArray();
        }

        // —— 破坏现场：换卡片、改配置、删图库记录与文件、删模型目录、放备份外的垃圾文件 ——
        host.Fsql.Delete<CardEntity>().Where(c => c.Id > 0).ExecuteAffrows();
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "intruder", Type = "test", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();
        await host.Settings.UpdateAsync(new UpdateSettingsRequest(
            "被破坏", 12, 1028,
            false, null, 0,
            0, "#000000", [],
            false, null, null, false,
            null, true, false,
            null));
        host.Fsql.Delete<ImageEntity>().Where(i => i.Id > 0).ExecuteAffrows();
        host.Fsql.Delete<Live2DModelConfigEntity>()
            .Where(c => c.ModelPath != string.Empty).ExecuteAffrows();
        File.Delete(Path.Combine(host.Paths.UploadsDirectory, "images", "photo.png"));
        Directory.Delete(Path.Combine(host.Paths.UploadsDirectory, "live2d"), true);
        WriteUploadFile(host, Path.Combine("images", "rogue.png"), [1, 2, 3]);

        // —— 恢复 ——
        var response = await host.Backup.RestoreAsync(TestZip.ToFormFile(archive));

        // 数据库记录一致
        Assert.Equal(cardsBefore.Count, response.Cards);
        Assert.Equal(imagesBefore.Count, response.Images);
        // 5 = images/photo.png + icons/site.ico + 模型的 3 个文件
        Assert.Equal(5, response.Files);

        var settingsAfter = await host.Settings.GetAsync();
        Assert.Equal(ToJson(settingsBefore), ToJson(settingsAfter));

        var cardsAfter = host.Fsql.Select<CardEntity>().OrderBy(c => c.Uuid).ToList();
        Assert.Equal(
            cardsBefore.Select(Describe),
            cardsAfter.Select(Describe));
        // 入侵者卡片被清掉，只剩备份里的两张
        Assert.Equal(2, cardsAfter.Count);

        var groupsAfter = host.Fsql.Select<CardGroupEntity>()
            .OrderBy(g => new { g.SortOrder, g.Id }).ToList();
        Assert.Equal(
            groupsBefore.Select(g => (g.Uuid, g.Name, g.SortOrder, g.Collapsed)),
            groupsAfter.Select(g => (g.Uuid, g.Name, g.SortOrder, g.Collapsed)));
        // 分组整体替换后，卡片仍归属原分组
        Assert.Equal("group-1", cardsAfter.Single(c => c.Uuid == "card-1").GroupUuid);
        Assert.Equal("group-2", cardsAfter.Single(c => c.Uuid == "card-2").GroupUuid);

        var imagesAfter = host.Fsql.Select<ImageEntity>().OrderBy(i => i.RelativePath).ToList();
        Assert.Equal(
            imagesBefore.Select(i => (i.RelativePath, i.OriginalName, i.Kind, i.Name)),
            imagesAfter.Select(i => (i.RelativePath, i.OriginalName, i.Kind, i.Name)));
        // 创建时间经清单 JSON 往返，允许 SQLite 存储的毫秒级精度差
        Assert.All(imagesAfter.Zip(imagesBefore),
            pair => Assert.True(
                Math.Abs((pair.First.CreatedAt - pair.Second.CreatedAt).TotalSeconds) < 1,
                $"CreatedAt 往返后偏差过大：{pair.First.CreatedAt} vs {pair.Second.CreatedAt}"));

        // UpdatedAt 由恢复时刻生成，不参与比较
        var live2dAfter = host.Fsql.Select<Live2DModelConfigEntity>().OrderBy(c => c.ModelPath).ToList();
        Assert.Equal(
            live2dBefore.Select(c => (c.ModelPath, c.EntryMotionId, c.IdleMotionId,
                c.Expression, c.Outfits, c.SoundEnabled, c.Scale,
                c.PositionX, c.PositionY, c.FreeRatioX, c.FreeRatioY)),
            live2dAfter.Select(c => (c.ModelPath, c.EntryMotionId, c.IdleMotionId,
                c.Expression, c.Outfits, c.SoundEnabled, c.Scale,
                c.PositionX, c.PositionY, c.FreeRatioX, c.FreeRatioY)));

        // 上传文件逐字节还原，垃圾文件被清走，且 uploads 下没有多余内容
        Assert.Equal(photoBytes,
            await File.ReadAllBytesAsync(Path.Combine(host.Paths.UploadsDirectory, "images", "photo.png")));
        Assert.Equal(iconBytes,
            await File.ReadAllBytesAsync(Path.Combine(host.Paths.UploadsDirectory, "icons", "site.ico")));
        Assert.Equal(modelJson,
            await File.ReadAllBytesAsync(Path.Combine(host.Paths.UploadsDirectory, "live2d", "hiyori",
                "hiyori.model3.json")));
        Assert.Equal(motionJson,
            await File.ReadAllBytesAsync(Path.Combine(host.Paths.UploadsDirectory, "live2d", "hiyori", "motions",
                "idle.motion3.json")));
        Assert.Equal(textureBytes,
            await File.ReadAllBytesAsync(Path.Combine(host.Paths.UploadsDirectory, "live2d", "hiyori",
                "hiyori.texture.png")));
        Assert.False(File.Exists(Path.Combine(host.Paths.UploadsDirectory, "images", "rogue.png")),
            "备份外的垃圾文件应被恢复流程清理");
        Assert.Equal(
            new[]
            {
                "icons/site.ico",
                "images/photo.png",
                "live2d/hiyori/hiyori.model3.json",
                "live2d/hiyori/hiyori.texture.png",
                "live2d/hiyori/motions/idle.motion3.json"
            },
            ListUploadFiles(host));
    }

    [Fact]
    public async Task 导出时数据库中指向丢失文件的图库记录不进备份()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        WriteUploadFile(host, Path.Combine("images", "exists.png"), [9, 9, 9]);
        host.Fsql.Insert(new ImageEntity
        {
            RelativePath = "uploads/images/exists.png", OriginalName = "exists.png",
            Kind = ImageService.KindImage
        }).ExecuteAffrows();
        // 文件已丢失的记录：导出时必须排除，否则恢复时必然失败
        host.Fsql.Insert(new ImageEntity
        {
            RelativePath = "uploads/images/ghost.png", OriginalName = "ghost.png",
            Kind = ImageService.KindImage
        }).ExecuteAffrows();

        byte[] archive;
        await using (var export = await host.Backup.CreateExportArchiveAsync())
        {
            using var copy = new MemoryStream();
            await export.CopyToAsync(copy);
            archive = copy.ToArray();
        }

        // 直接读取导出包内的 manifest.json，固化导出语义：清单 camelCase、只含真实存在的图片
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        await using var manifestStream = zip.GetEntry(TestZip.ManifestEntryName)!.Open();
        var manifest = JsonNode.Parse(manifestStream)!;
        Assert.Equal(BackupLimits.CurrentVersion, (int)manifest["version"]!);
        Assert.Equal(
            ["uploads/images/exists.png"],
            manifest["images"]!.AsArray().Select(i => i!["relativePath"]!.GetValue<string>()).ToArray());
        Assert.Equal(
            [],
            manifest["cards"]!.AsArray().Select(i => i!["uuid"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public async Task 恢复时清单引用包内缺失的图片文件应整体拒绝且不改动数据()
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

        // 清单声明了一张图库图片，但包里没有对应文件——恢复必须整体拒绝
        var manifest = """
                       {"version":1,"settings":{"siteTitle":"atlas"},"live2D":{"configs":[]},"cards":[],
                        "images":[{"relativePath":"uploads/images/ghost.png","originalName":"ghost.png","kind":"image","createdAt":"2026-01-01T00:00:00Z"}]}
                       """;
        var zip = TestZip.CreateArchive((TestZip.ManifestEntryName, TestZip.Utf8(manifest)));

        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            host.Backup.RestoreAsync(TestZip.ToFormFile(zip)));
        Assert.Contains("缺少图片文件", error.Message);

        // 校验发生在解包与落库之前，现有数据不受影响
        Assert.Equal("原配置", (await host.Settings.GetAsync()).SiteTitle);
    }

    /// <summary>卡片逐字段投影；Data 用 ToJsonString 归一化，规避 JSON 往返时的空格差异</summary>
    private static (string Uuid, int SmX, int SmY, int SmWidth, int SmHeight, int LgX, int LgY,
        int LgWidth, int LgHeight, int MinWidth, int MinHeight, bool IsVisible, bool? MobileVisible,
        string Type, string? GroupUuid, string? Data) Describe(CardEntity card)
    {
        return (card.Uuid, card.SmX, card.SmY, card.SmWidth, card.SmHeight,
            card.LgX, card.LgY, card.LgWidth, card.LgHeight,
            card.MinWidth, card.MinHeight, card.IsVisible, card.MobileVisible, card.Type,
            card.GroupUuid,
            JsonNode.Parse(card.Data ?? "null")?.ToJsonString());
    }

    private static void WriteUploadFile(TestHost host, string relativePath, byte[] content)
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

    /// <summary>用统一序列化比较站点配置：SettingsResponse 未实现值相等，JSON 逐字段对账最直观</summary>
    private static string ToJson(SettingsResponse value)
    {
        return JsonSerializer.Serialize(value,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static byte[] DeterministicBytes(int length, byte seed)
    {
        return Enumerable.Range(0, length).Select(i => (byte)((i + seed) & 0xFF)).ToArray();
    }
}