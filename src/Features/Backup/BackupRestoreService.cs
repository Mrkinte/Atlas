using System.IO.Compression;
using Atlas.Extensions;
using Atlas.Features.Backup.Contracts;
using Atlas.Features.Backup.Migrations;
using Atlas.Features.Card.Entities;
using Atlas.Features.Image.Entities;
using Atlas.Features.Live2D;
using Atlas.Features.Live2D.Entities;
using Atlas.Features.Settings;
using Atlas.Features.Settings.Contracts;
using Atlas.Features.Settings.Entities;
using Atlas.Features.SystemStatus;

namespace Atlas.Features.Backup;

/// <summary>
///     备份恢复：先校验版本与压缩包内容，再按选中的分段覆盖数据库记录与上传文件
///     清单读取与内容校验见 <see cref="BackupManifestBuilder" />
/// </summary>
public class BackupRestoreService(
    IFreeSql fsql,
    SettingsService settingsService,
    SystemStatusRuntime systemStatus,
    AtlasPaths paths,
    BackupManifestMigrator manifestMigrator)
{
    public async Task<BackupRestoreResponse> RestoreAsync(
        IFormFile file,
        BackupSection sections = BackupSection.All,
        CancellationToken ct = default)
    {
        if (file.Length <= 0 || file.Length > BackupLimits.MaxRestoreArchiveBytes)
            throw new InvalidRequestException("备份文件为空或超过 512 MB");

        var tempPath = await BackupTempArchive.SaveAsync(file, "atlas-restore", ct);

        try
        {
            await using var stream = File.OpenRead(tempPath);
            await using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var manifest = await BackupManifestBuilder.LoadManifestAsync(
                archive, manifestMigrator, ct);

            var missing = sections & ~BackupSectionNames.FromNames(manifest.Sections);
            if (missing != BackupSection.None)
                throw new InvalidRequestException(
                    $"备份中不包含{BackupSectionNames.Describe(missing)}内容");

            var uploadEntries = CollectUploadEntries(archive, sections);
            if (sections.HasFlag(BackupSection.Images))
                ValidateImageEntries(manifest, uploadEntries);

            await ExtractFilesAsync(uploadEntries, ct);
            var response = await RestoreDatabaseAsync(manifest, sections, uploadEntries, ct);
            DeleteUnreferencedFiles(uploadEntries.Keys, sections);

            return response;
        }
        catch (InvalidDataException)
        {
            throw new InvalidRequestException("备份文件不是有效的 ZIP");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static Dictionary<string, ZipArchiveEntry> CollectUploadEntries(
        ZipArchive archive, BackupSection sections)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(
            StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0L;

        foreach (var entry in archive.Entries)
        {
            var path = ZipArchiveSafety.NormalizeRelativePath(entry.FullName);

            var isLive2D = BackupUploadPaths.TryResolveLive2DEntry(path, out _);
            if (isLive2D)
            {
                if (!sections.HasFlag(BackupSection.Live2D)) continue;
            }
            else
            {
                // 只收集图片与模型目录下的文件，目录条目会被规则排除
                if (!sections.HasFlag(BackupSection.Images)) continue;
                if (!BackupUploadPaths.TryResolveUploadEntry(path, out _, out _)) continue;
            }

            var limit = isLive2D ? BackupLimits.MaxLive2DEntryBytes : BackupLimits.MaxEntryBytes;
            if (entry.Length > limit) throw new InvalidRequestException($"备份中存在过大的文件：{path}");

            totalBytes += entry.Length;
            if (totalBytes > BackupLimits.MaxTotalUncompressedBytes) throw new InvalidRequestException("备份文件解压后过大");

            if (!entries.TryAdd(path, entry)) throw new InvalidRequestException($"备份中存在重复文件：{path}");
        }

        return entries;
    }

    private static void ValidateImageEntries(
        BackupManifest manifest,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        foreach (var image in manifest.Images!)
        {
            var path = ZipArchiveSafety.NormalizeRelativePath(image.RelativePath);
            if (!entries.ContainsKey(path)) throw new InvalidRequestException($"备份缺少图片文件：{image.RelativePath}");
        }
    }

    private async Task ExtractFilesAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        CancellationToken ct)
    {
        var uploadsDirectory = paths.UploadsDirectory;
        Directory.CreateDirectory(uploadsDirectory);

        foreach (var (path, entry) in entries)
        {
            ct.ThrowIfCancellationRequested();

            // 条目路径都带 uploads/ 前缀，落盘前必须去掉，否则会多出一层 uploads 目录
            var isLive2D = BackupUploadPaths.TryResolveLive2DEntry(path, out _);
            var destinationRelative = path[BackupUploadPaths.UploadsPrefix.Length..];

            var segments = destinationRelative.Split('/');
            var destination = Path.Combine(
                uploadsDirectory,
                string.Join(Path.DirectorySeparatorChar, segments));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            var limit = isLive2D ? BackupLimits.MaxLive2DEntryBytes : BackupLimits.MaxEntryBytes;

            // 先写临时文件再覆盖，避免中途失败留下半截文件
            var tempPath = destination + ".restoretmp";
            try
            {
                await using (var source = entry.Open())
                await using (var target = File.Create(tempPath))
                {
                    await CopyWithLimitAsync(source, target, limit, ct);
                }

                File.Move(tempPath, destination, true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }

    /// <summary>按实际写入字节数限制单个文件，防止 ZIP 头部声明的解压大小造假</summary>
    private static async Task CopyWithLimitAsync(
        Stream source, Stream target, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920];
        var total = 0L;

        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) break;

            total += read;
            if (total > limit) throw new InvalidRequestException("备份中存在解压后过大的文件");

            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private async Task<BackupRestoreResponse> RestoreDatabaseAsync(
        BackupManifest manifest,
        BackupSection sections,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        CancellationToken ct)
    {
        var cardGroupEntities = sections.HasFlag(BackupSection.Cards)
                                && manifest.CardGroups is not null
            ? manifest.CardGroups
                .Select(group => new CardGroupEntity
                {
                    Uuid = group.Uuid,
                    Name = group.Name,
                    SortOrder = group.SortOrder,
                    Collapsed = group.Collapsed
                })
                .OrderBy(g => g.SortOrder)
                .ToList()
            : null;

        string? defaultGroupUuid = null;
        if (sections.HasFlag(BackupSection.Cards))
        {
            // 备份不带分组时沿用现有分组；备份分组会被整体替换，不能引用现有分组
            var defaultGroup = cardGroupEntities is not null
                ? cardGroupEntities.FirstOrDefault()
                : await fsql.Select<CardGroupEntity>()
                    .OrderBy(g => new { g.SortOrder, g.Id })
                    .FirstAsync(ct);

            if (defaultGroup is null)
            {
                defaultGroup = new CardGroupEntity
                {
                    Uuid = Guid.NewGuid().ToString("N"),
                    Name = string.Empty,
                    SortOrder = 0,
                    Collapsed = false
                };
                cardGroupEntities ??= [];
                cardGroupEntities.Add(defaultGroup);
            }

            defaultGroupUuid = defaultGroup.Uuid;
        }

        var cardEntities = sections.HasFlag(BackupSection.Cards)
            ? manifest.Cards!.Select(card => new CardEntity
            {
                Uuid = card.Uuid,
                SmX = card.SmX,
                SmY = card.SmY,
                SmWidth = card.SmWidth,
                SmHeight = card.SmHeight,
                LgX = card.LgX,
                LgY = card.LgY,
                LgWidth = card.LgWidth,
                LgHeight = card.LgHeight,
                MinWidth = card.MinWidth,
                MinHeight = card.MinHeight,
                IsVisible = card.IsVisible,
                MobileVisible = card.MobileVisible,
                Type = card.Type,
                GroupUuid = string.IsNullOrWhiteSpace(card.GroupUuid)
                    ? defaultGroupUuid
                    : card.GroupUuid,
                Data = card.Data?.ToJsonString()
            }).ToList()
            : [];

        var imageEntities = sections.HasFlag(BackupSection.Images)
            ? manifest.Images!.Select(image =>
            {
                var path = ZipArchiveSafety.NormalizeRelativePath(image.RelativePath);
                return new ImageEntity
                {
                    Name = Path.GetFileName(path),
                    RelativePath = path,
                    OriginalName = image.OriginalName,
                    Kind = image.Kind,
                    CreatedAt = image.CreatedAt
                };
            }).ToList()
            : [];

        var live2DConfigEntities = sections.HasFlag(BackupSection.Live2D)
            ? BuildLive2DConfigEntities(manifest.Live2D!, entries)
            : [];

        var settingsEntity = sections.HasFlag(BackupSection.Settings)
            ? await BuildSettingsEntityAsync(manifest, sections, entries, ct)
            : null;

        fsql.Transaction(() =>
        {
            if (settingsEntity is not null)
            {
                fsql.Delete<SettingsEntity>().Where(s => s.Id == 1).ExecuteAffrows();
                fsql.Insert(settingsEntity).ExecuteAffrows();
            }

            if (sections.HasFlag(BackupSection.Cards))
            {
                if (manifest.CardGroups is not null)
                    fsql.Delete<CardGroupEntity>().Where(g => g.Id > 0).ExecuteAffrows();

                if (cardGroupEntities is { Count: > 0 })
                    fsql.Insert(cardGroupEntities).ExecuteAffrows();

                fsql.Delete<CardEntity>().Where(c => c.Id > 0).ExecuteAffrows();
                if (cardEntities.Count > 0) fsql.Insert(cardEntities).ExecuteAffrows();
            }

            if (sections.HasFlag(BackupSection.Images))
            {
                fsql.Delete<ImageEntity>().Where(i => i.Id > 0).ExecuteAffrows();
                if (imageEntities.Count > 0) fsql.Insert(imageEntities).ExecuteAffrows();
            }

            if (sections.HasFlag(BackupSection.Live2D))
            {
                fsql.Delete<Live2DModelConfigEntity>()
                    .Where(c => c.ModelPath != string.Empty)
                    .ExecuteAffrows();
                if (live2DConfigEntities.Count > 0)
                    fsql.Insert(live2DConfigEntities).ExecuteAffrows();
            }
        });

        if (sections.HasFlag(BackupSection.Live2D)
            && !sections.HasFlag(BackupSection.Settings))
            await UpdateLive2DSettingsAsync(manifest.Live2D!, entries, ct);

        // 恢复的配置可能改变系统状态开关，同步到运行期状态
        if (settingsEntity is not null)
            systemStatus.SetEnabled(settingsEntity.SystemStatusEnabled);

        return new BackupRestoreResponse(
            cardEntities.Count,
            imageEntities.Count,
            entries.Count,
            live2DConfigEntities.Count,
            settingsEntity is not null);
    }

    private async Task<SettingsEntity> BuildSettingsEntityAsync(
        BackupManifest manifest,
        BackupSection sections,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        CancellationToken ct)
    {
        var settings = manifest.Settings!;
        var entity = new SettingsEntity
        {
            Id = 1,
            SiteTitle = settings.SiteTitle,
            DesktopCols = settings.DesktopCols,
            DesktopGridContainerMaxWidth = settings.DesktopGridContainerMaxWidth,
            DesktopGridFullWidth = settings.DesktopGridFullWidth,
            BackgroundImage = settings.BackgroundImage,
            BackgroundBlur = settings.BackgroundBlur,
            BackgroundMask = settings.BackgroundMask,
            PrimaryColor = settings.PrimaryColor,
            PublicHardware = SettingsService.SerializePublicHardware(settings.PublicHardware),
            SystemStatusEnabled = settings.SystemStatusEnabled,
            DockerEnabled = settings.DockerEnabled ?? true,
            DockerSocket = SettingsService.NormalizeDockerSocket(settings.DockerSocket),
            SiteSubtitle = settings.SiteSubtitle ?? string.Empty,
            Favicon = settings.Favicon ?? string.Empty,
            ShowHeaderLogo = settings.ShowHeaderLogo,
            FooterText = settings.FooterText ?? string.Empty,
            ServiceProbeEnabled = settings.ServiceProbeEnabled
        };

        if (sections.HasFlag(BackupSection.Live2D))
        {
            entity.Live2DEnabled = manifest.Live2D!.Enabled;
            entity.Live2DModelPath = ResolveLive2DModelPath(manifest.Live2D, entries);
            return entity;
        }

        // 未恢复 Live2D 时保留当前开关与模型，避免站点设置单独恢复后模型状态失配
        var current = await fsql.Select<SettingsEntity>()
            .Where(s => s.Id == 1)
            .FirstAsync(ct);
        entity.Live2DEnabled = current?.Live2DEnabled ?? false;
        entity.Live2DModelPath = current?.Live2DModelPath;
        return entity;
    }

    private static List<Live2DModelConfigEntity> BuildLive2DConfigEntities(
        BackupLive2D live2D,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        return live2D.Configs
            .Select(config =>
            {
                var layout = Live2DConfigService.NormalizeLayout(
                    config.Scale, config.PositionX, config.PositionY,
                    config.FreeRatioX, config.FreeRatioY);

                return new Live2DModelConfigEntity
                {
                    ModelPath = SettingsService.NormalizeLive2DModelPath(config.ModelPath),
                    EntryMotionId = config.EntryMotionId?.Trim() ?? string.Empty,
                    IdleMotionId = config.IdleMotionId?.Trim() ?? string.Empty,
                    Expression = config.Expression?.Trim() ?? string.Empty,
                    Outfits = SettingsService.SerializeLive2DOutfits(config.Outfits),
                    SoundEnabled = config.SoundEnabled,
                    Scale = layout.Scale,
                    PositionX = layout.PositionX,
                    PositionY = layout.PositionY,
                    FreeRatioX = layout.FreeRatioX,
                    FreeRatioY = layout.FreeRatioY,
                    UpdatedAt = DateTime.UtcNow
                };
            })
            .Where(config => config.ModelPath.Length > 0)
            // 上传模型的配置只在包内存在对应文件时才恢复，避免指向不存在的模型
            .Where(config => BackupUploadPaths.IsLive2DConfigRestorable(
                config.ModelPath, entries))
            .ToList();
    }

    /// <summary>
    ///     只恢复 Live2D 时站点设置保持原值，仅覆盖开关与当前模型；
    ///     没有配置行时由 SettingsService 写入默认值
    /// </summary>
    private async Task UpdateLive2DSettingsAsync(
        BackupLive2D live2D,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        CancellationToken ct)
    {
        var current = await settingsService.GetAsync(ct);

        await settingsService.UpdateAsync(new UpdateSettingsRequest(
            current.SiteTitle,
            current.DesktopCols,
            current.DesktopGridContainerMaxWidth,
            current.DesktopGridFullWidth,
            current.BackgroundImage,
            current.BackgroundBlur,
            current.BackgroundMask,
            current.PrimaryColor,
            current.PublicHardware,
            current.SystemStatusEnabled,
            current.SiteSubtitle,
            current.Favicon,
            current.ShowHeaderLogo,
            current.FooterText,
            current.ServiceProbeEnabled,
            live2D.Enabled,
            ResolveLive2DModelPath(live2D, entries),
            current.DockerEnabled,
            current.DockerSocket), ct);
    }

    /// <summary>包内缺少对应文件时回退内置模型</summary>
    private static string? ResolveLive2DModelPath(
        BackupLive2D live2D,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        return BackupUploadPaths.ResolveRestoredModelPath(
            SettingsService.NormalizeLive2DModelPath(live2D.ModelPath), entries);
    }

    /// <summary>只清理选中分段覆盖的目录，未恢复的分段不受影响</summary>
    private void DeleteUnreferencedFiles(
        IReadOnlyCollection<string> keep, BackupSection sections)
    {
        var uploadsDirectory = paths.UploadsDirectory;
        var keepSet = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);

        if (sections.HasFlag(BackupSection.Images))
            foreach (var folder in BackupUploadPaths.UploadFolders)
            {
                var directory = Path.Combine(uploadsDirectory, folder);
                if (!Directory.Exists(directory)) continue;

                foreach (var fullPath in Directory.EnumerateFiles(directory))
                {
                    var relativePath =
                        $"{BackupUploadPaths.UploadsPrefix}{folder}/{Path.GetFileName(fullPath)}";
                    if (keepSet.Contains(relativePath)) continue;

                    try
                    {
                        File.Delete(fullPath);
                    }
                    catch (Exception e) when (
                        e is IOException or UnauthorizedAccessException)
                    {
                        // 孤立文件不影响恢复，忽略删除失败
                    }
                }
            }

        if (sections.HasFlag(BackupSection.Live2D))
            DeleteUnreferencedLive2DFiles(uploadsDirectory, keepSet);
    }

    /// <summary>清掉备份中不存在的模型文件，并移除因此变空的模型目录</summary>
    private static void DeleteUnreferencedLive2DFiles(
        string uploadsDirectory, IReadOnlySet<string> keepSet)
    {
        var directory = Path.Combine(uploadsDirectory, BackupUploadPaths.Live2DFolder);
        if (!Directory.Exists(directory)) return;

        foreach (var fullPath in Directory
                     .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .ToArray())
        {
            var relativePath = $"{BackupUploadPaths.UploadsPrefix}{ZipArchiveSafety.NormalizeRelativePath(
                Path.GetRelativePath(uploadsDirectory, fullPath))}";
            if (keepSet.Contains(relativePath)) continue;

            try
            {
                File.Delete(fullPath);
            }
            catch (Exception e) when (
                e is IOException or UnauthorizedAccessException)
            {
                // 孤立文件不影响恢复，忽略删除失败
            }
        }

        // 自底向上删除空目录，保留 live2d 根目录
        foreach (var empty in Directory
                     .EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .ToArray()
                     .OrderDescending())
            try
            {
                if (Directory.EnumerateFileSystemEntries(empty).Any()) continue;
                Directory.Delete(empty);
            }
            catch (Exception e) when (
                e is IOException or UnauthorizedAccessException)
            {
            }
    }
}