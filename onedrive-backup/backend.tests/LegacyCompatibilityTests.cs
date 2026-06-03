using System.Text.Json;
using HassioOneDriveBackup.Models;
using HassioOneDriveBackup.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph.Models;
using Moq;

namespace HassioOneDriveBackup.Tests;

/// <summary>
/// Tests covering the three legacy backward-compatibility layers that support upgrading
/// from the old dev-branch (2.x) addon to ng-alpha:
///
///   A. Legacy OneDrive filename parsing  — flat root files: {name}_{slug}[.instance].tar
///   B. Retention flag migration          — additionalBackupData.json → retained_backups.json
///   C. Legacy OneDrive detection         — orchestrator skips re-uploading already-uploaded backups
///   D. Settings migration                — flat snake_case JSON → nested PascalCase Settings
/// </summary>
public class LegacyCompatibilityTests
{
    // =========================================================================
    // Region A — Legacy filename parsing (ExtractSlugFromLegacyFileName)
    // =========================================================================

    [Theory]
    [InlineData("HassBackup_abc123.tar", "abc123")]
    [InlineData("hass_backup_def456.tar", "def456")]
    [InlineData("My_Custom_Backup_def456.tar", "def456")]
    public void ExtractSlug_StandardPattern_ReturnsSlug(string fileName, string expected)
    {
        var result = BackupOrchestratorService.ExtractSlugFromLegacyFileName(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("HassBackup_abc123.my-instance.tar", "abc123")]
    [InlineData("HassBackup_abc123.inst.name.tar", "abc123")]
    [InlineData("Backup_def456.home-assistant.tar", "def456")]
    public void ExtractSlug_WithInstanceName_StripsInstanceReturnsSlug(string fileName, string expected)
    {
        var result = BackupOrchestratorService.ExtractSlugFromLegacyFileName(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("abc123.tar")]          // no underscore
    [InlineData("justname.tar")]        // no underscore
    public void ExtractSlug_NoUnderscore_ReturnsNull(string fileName)
    {
        var result = BackupOrchestratorService.ExtractSlugFromLegacyFileName(fileName);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("HassBackup_.tar")]         // empty slug
    [InlineData("HassBackup_.my-inst.tar")] // empty slug before instance
    public void ExtractSlug_EmptySlug_ReturnsNull(string fileName)
    {
        var result = BackupOrchestratorService.ExtractSlugFromLegacyFileName(fileName);
        Assert.Null(result);
    }

    // =========================================================================
    // Region B — Retention flag migration (RetentionDataStore)
    // =========================================================================

    private static RetentionDataStore MakeStore(string configDir, string dataDir)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigFolder"] = configDir,
                ["DataFolder"] = dataDir,
            })
            .Build();
        return new RetentionDataStore(config, NullLogger<RetentionDataStore>.Instance);
    }

    private static (string configDir, string dataDir) MakeTempDirs()
    {
        var configDir = Path.Combine(Path.GetTempPath(), $"legacy_cfg_{Guid.NewGuid():N}");
        var dataDir = Path.Combine(Path.GetTempPath(), $"legacy_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(dataDir);
        return (configDir, dataDir);
    }

    private static void WriteLegacyRetentionFile(string configDir, object backups)
    {
        var json = JsonSerializer.Serialize(new { Backups = backups });
        File.WriteAllText(Path.Combine(configDir, "additionalBackupData.json"), json);
    }

    [Fact]
    public void RetentionMigration_RetainLocal_True_SlugMigrated()
    {
        var (configDir, dataDir) = MakeTempDirs();
        WriteLegacyRetentionFile(configDir, new[]
        {
            new { Slug = "abc123", RetainLocal = true, RetainOneDrive = false }
        });

        var store = MakeStore(configDir, dataDir);

        Assert.Contains("abc123", store.GetRetainedSlugs());
    }

    [Fact]
    public void RetentionMigration_RetainOneDrive_True_SlugMigrated()
    {
        var (configDir, dataDir) = MakeTempDirs();
        WriteLegacyRetentionFile(configDir, new[]
        {
            new { Slug = "def456", RetainLocal = false, RetainOneDrive = true }
        });

        var store = MakeStore(configDir, dataDir);

        Assert.Contains("def456", store.GetRetainedSlugs());
    }

    [Fact]
    public void RetentionMigration_BothFalse_SlugNotMigrated()
    {
        var (configDir, dataDir) = MakeTempDirs();
        WriteLegacyRetentionFile(configDir, new[]
        {
            new { Slug = "xyz789", RetainLocal = false, RetainOneDrive = false }
        });

        var store = MakeStore(configDir, dataDir);

        Assert.DoesNotContain("xyz789", store.GetRetainedSlugs());
    }

    [Fact]
    public void RetentionMigration_MultipleEntries_OnlyRetainedMigrated()
    {
        var (configDir, dataDir) = MakeTempDirs();
        WriteLegacyRetentionFile(configDir, new object[]
        {
            new { Slug = "keep1", RetainLocal  = true,  RetainOneDrive = false },
            new { Slug = "keep2", RetainLocal  = false, RetainOneDrive = true  },
            new { Slug = "drop1", RetainLocal  = false, RetainOneDrive = false },
            new { Slug = "drop2", RetainLocal  = false, RetainOneDrive = false },
        });

        var store = MakeStore(configDir, dataDir);
        var slugs = store.GetRetainedSlugs();

        Assert.Contains("keep1", slugs);
        Assert.Contains("keep2", slugs);
        Assert.DoesNotContain("drop1", slugs);
        Assert.DoesNotContain("drop2", slugs);
    }

    [Fact]
    public void RetentionMigration_AfterMigration_FileRenamedToMigrated()
    {
        var (configDir, dataDir) = MakeTempDirs();
        var legacyPath = Path.Combine(configDir, "additionalBackupData.json");
        WriteLegacyRetentionFile(configDir, new[]
        {
            new { Slug = "abc", RetainLocal = true, RetainOneDrive = false }
        });

        _ = MakeStore(configDir, dataDir);

        Assert.False(File.Exists(legacyPath), "Original file should be renamed after migration");
        Assert.True(File.Exists(legacyPath + ".migrated"), ".migrated file should exist");
    }

    [Fact]
    public void RetentionMigration_AlreadyMigrated_NoReRun_ReturnsEmpty()
    {
        var (configDir, dataDir) = MakeTempDirs();
        // Only the .migrated file exists — no re-run should happen
        File.WriteAllText(
            Path.Combine(configDir, "additionalBackupData.json.migrated"),
            JsonSerializer.Serialize(new { Backups = new[] { new { Slug = "s1", RetainLocal = true, RetainOneDrive = false } } }));

        var store = MakeStore(configDir, dataDir);

        Assert.Empty(store.GetRetainedSlugs());
    }

    [Fact]
    public void RetentionMigration_MalformedJson_DoesNotThrow_ReturnsEmpty()
    {
        var (configDir, dataDir) = MakeTempDirs();
        File.WriteAllText(Path.Combine(configDir, "additionalBackupData.json"), "{ this is not valid JSON !!!");

        var store = MakeStore(configDir, dataDir); // must not throw

        Assert.Empty(store.GetRetainedSlugs());
    }

    // =========================================================================
    // Region C — Legacy OneDrive detection (prevents re-upload after upgrade)
    // =========================================================================

    /// <summary>
    /// Directly tests ExtractSlugFromLegacyFileName and verifies the orchestrator's
    /// AddLegacyOneDriveSlugsAsync (via reflection) correctly populates the slug set.
    /// </summary>
    private static BackupOrchestratorService MakeOrchestrator(
        Mock<IOneDriveClient> oneDrive,
        Mock<IHassioClient>? hassio = null,
        Mock<IBackupService>? backupSvc = null)
    {
        hassio ??= new Mock<IHassioClient>();
        backupSvc ??= new Mock<IBackupService>();
        var settings = new Mock<ISettingsService>();
        var retention = new Mock<IRetentionPolicyService>();
        var dateTime = new Mock<IDateTimeProvider>();
        var entityState = new HassEntityStateService(hassio.Object, NullLogger<HassEntityStateService>.Instance);

        hassio.Setup(h => h.UpdateHassEntityStateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var tempDir = Path.Combine(Path.GetTempPath(), $"orch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataFolder"] = tempDir, ["ConfigFolder"] = tempDir })
            .Build();
        var retentionStore = new RetentionDataStore(config, NullLogger<RetentionDataStore>.Instance);

        return new BackupOrchestratorService(
            hassio.Object,
            oneDrive.Object,
            backupSvc.Object,
            settings.Object,
            retention.Object,
            retentionStore,
            dateTime.Object,
            entityState,
            NullLogger<BackupOrchestratorService>.Instance);
    }

    private static async Task InvokeAddLegacySlugsAsync(BackupOrchestratorService orchestrator, HashSet<string> slugSet)
    {
        var method = typeof(BackupOrchestratorService).GetMethod(
            "AddLegacyOneDriveSlugsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("AddLegacyOneDriveSlugsAsync not found");

        await (Task)method.Invoke(orchestrator, new object[] { slugSet })!;
    }

    [Fact]
    public async Task LegacyOneDrive_FlatRootFile_SlugExtracted()
    {
        var oneDrive = new Mock<IOneDriveClient>();
        oneDrive.Setup(o => o.ListFilesInDirectoryAsync(string.Empty))
            .ReturnsAsync(new List<DriveItem>
            {
                new() { Name = "HassBackup_abc123.tar" }
            });

        var orchestrator = MakeOrchestrator(oneDrive);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await InvokeAddLegacySlugsAsync(orchestrator, slugs);

        Assert.Contains("abc123", slugs);
    }

    [Fact]
    public async Task LegacyOneDrive_FlatRootFileWithInstance_SlugExtracted()
    {
        var oneDrive = new Mock<IOneDriveClient>();
        oneDrive.Setup(o => o.ListFilesInDirectoryAsync(string.Empty))
            .ReturnsAsync(new List<DriveItem>
            {
                new() { Name = "HassBackup_def456.my-instance.tar" }
            });

        var orchestrator = MakeOrchestrator(oneDrive);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await InvokeAddLegacySlugsAsync(orchestrator, slugs);

        Assert.Contains("def456", slugs);
    }

    [Fact]
    public async Task LegacyOneDrive_MalformedFilename_NotAdded_NoError()
    {
        var oneDrive = new Mock<IOneDriveClient>();
        oneDrive.Setup(o => o.ListFilesInDirectoryAsync(string.Empty))
            .ReturnsAsync(new List<DriveItem>
            {
                new() { Name = "abc123.tar" }  // no underscore — not a legacy backup
            });

        var orchestrator = MakeOrchestrator(oneDrive);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await InvokeAddLegacySlugsAsync(orchestrator, slugs); // must not throw

        Assert.Empty(slugs);
    }

    [Fact]
    public async Task LegacyOneDrive_RootUnavailable_DoesNotThrow_SlugSetUnchanged()
    {
        var oneDrive = new Mock<IOneDriveClient>();
        oneDrive.Setup(o => o.ListFilesInDirectoryAsync(string.Empty))
            .ThrowsAsync(new HttpRequestException("Simulated network failure"));

        var orchestrator = MakeOrchestrator(oneDrive);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "existing" };

        await InvokeAddLegacySlugsAsync(orchestrator, slugs); // must not throw

        // Pre-existing slugs must survive
        Assert.Contains("existing", slugs);
    }

    [Fact]
    public async Task LegacyOneDrive_MultipleFilesAtRoot_AllValidSlugsExtracted()
    {
        var oneDrive = new Mock<IOneDriveClient>();
        oneDrive.Setup(o => o.ListFilesInDirectoryAsync(string.Empty))
            .ReturnsAsync(new List<DriveItem>
            {
                new() { Name = "HassBackup_slug1.tar" },
                new() { Name = "HassBackup_slug2.my-host.tar" },
                new() { Name = "notabackup.tar" },      // no underscore — ignored
                new() { Name = "README.md" },           // not a tar — ignored
            });

        var orchestrator = MakeOrchestrator(oneDrive);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await InvokeAddLegacySlugsAsync(orchestrator, slugs);

        Assert.Contains("slug1", slugs);
        Assert.Contains("slug2", slugs);
        Assert.Equal(2, slugs.Count);
    }

    // =========================================================================
    // Region D — Settings migration (SettingsService legacy format detection)
    // =========================================================================

    private static SettingsService MakeSettingsService(string dataDir)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataFolder"] = dataDir })
            .Build();

        var oneDrive = new Mock<IOneDriveClient>();
        return new SettingsService(NullLogger<SettingsService>.Instance, oneDrive.Object, config);
    }

    private static string MakeLegacySettingsJson(object? overrides = null)
    {
        // Matches the dev-branch AddonOptions flat snake_case JSON schema
        var defaults = new Dictionary<string, object?>
        {
            ["local_backup_num_to_keep"] = 5,
            ["onedrive_backup_num_to_keep"] = 15,
            ["backup_interval_days"] = 2,
            ["backup_name"] = "hass_backup",
            ["backup_instance_name"] = "my-ha",
            ["notify_on_error"] = true,
            ["hass_api_timeout_minutes"] = 30,
            ["exclude_media_folder"] = false,
            ["exclude_ssl_folder"] = false,
            ["exclude_share_folder"] = false,
            ["exclude_local_addons_folder"] = false,
            ["monitor_all_local_backups"] = true,
            ["sync_paths"] = new List<string> { "/config/zigbee2mqtt" },
            ["file_sync_remove_deleted"] = false,
            ["ignore_allowed_hours_for_file_sync"] = false,
            ["excluded_addons"] = new List<string>(),
            ["log_level"] = "info",
            ["ignore_upgrade_backups"] = false,
            ["enable_anonymous_telemetry"] = false,
            ["enable_anonymous_error_reporting"] = false,
        };

        if (overrides is System.Collections.IDictionary dict)
            foreach (System.Collections.DictionaryEntry kv in dict)
                defaults[(string)kv.Key] = kv.Value;

        return JsonSerializer.Serialize(defaults);
    }

    [Fact]
    public async Task LegacySettings_MaxLocalBackups_CarriedOver()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), MakeLegacySettingsJson());

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Equal(5, s.Backup.MaxLocalBackups);
    }

    [Fact]
    public async Task LegacySettings_MaxOneDriveBackups_CarriedOver()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), MakeLegacySettingsJson());

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Equal(15, s.Backup.MaxOnedriveBackups);
    }

    [Fact]
    public async Task LegacySettings_InstanceName_CarriedOver()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), MakeLegacySettingsJson());

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Equal("my-ha", s.General.InstanceName);
    }

    [Fact]
    public async Task LegacySettings_BackupIntervalDays_CarriedOver()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), MakeLegacySettingsJson());

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Equal(2, s.Backup.BackupIntervalDays);
    }

    [Fact]
    public async Task LegacySettings_GenerationalDays_CarriedOver()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["local_backup_num_to_keep"] = 5,
            ["onedrive_backup_num_to_keep"] = 10,
            ["backup_interval_days"] = 1,
            ["backup_instance_name"] = "test",
            ["generational_days"] = 7,
            ["generational_weeks"] = 4,
            ["generational_months"] = 3,
            ["generational_years"] = 1,
        });
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), json);

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Equal(7, s.Backup.GenerationalDays);
        Assert.Equal(4, s.Backup.GenerationalWeeks);
        Assert.Equal(3, s.Backup.GenerationalMonths);
        Assert.Equal(1, s.Backup.GenerationalYears);
    }

    [Fact]
    public async Task LegacySettings_SyncPaths_CarriedOver()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), MakeLegacySettingsJson());

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Single(s.FileSync.SyncPaths);
        Assert.Equal("/config/zigbee2mqtt", s.FileSync.SyncPaths[0]);
    }

    [Fact]
    public async Task LegacySettings_AfterMigration_FileWrittenInNewFormat()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        var settingsPath = Path.Combine(dataDir, "settings.json");
        File.WriteAllText(settingsPath, MakeLegacySettingsJson());

        _ = MakeSettingsService(dataDir);

        // File should now be valid new-format Settings JSON
        var newJson = File.ReadAllText(settingsPath);
        var reloaded = JsonSerializer.Deserialize<Settings>(newJson);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.General);
        Assert.NotNull(reloaded.Backup);
        Assert.Equal("my-ha", reloaded.General.InstanceName);
        // New format must NOT contain legacy snake_case keys
        Assert.DoesNotContain("local_backup_num_to_keep", newJson);
    }

    [Fact]
    public async Task LegacySettings_NoFile_DefaultsApplied()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.NotNull(s);
        Assert.Equal(10, s.Backup.MaxLocalBackups);   // default
        Assert.Equal(20, s.Backup.MaxOnedriveBackups); // default
    }

    [Fact]
    public async Task LegacySettings_NewFormatAlreadyPresent_LoadedAsIs()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);

        var existing = new Settings
        {
            General = new GeneralSettings { InstanceName = "already-migrated" },
            Backup = new BackupSettings { MaxLocalBackups = 7, MaxOnedriveBackups = 14, BackupIntervalDays = 4 },
            FileSync = new FileSyncSettings()
        };
        File.WriteAllText(
            Path.Combine(dataDir, "settings.json"),
            JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));

        var svc = MakeSettingsService(dataDir);
        var s = await svc.GetSettingsAsync();

        Assert.Equal("already-migrated", s.General.InstanceName);
        Assert.Equal(7, s.Backup.MaxLocalBackups);
        Assert.Equal(14, s.Backup.MaxOnedriveBackups);
    }
}
