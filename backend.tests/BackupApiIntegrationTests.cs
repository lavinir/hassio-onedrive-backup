using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HassioOneDriveBackup.Models;
using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Services.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Moq;

namespace HassioOneDriveBackup.Tests;

/// <summary>
/// Integration tests that wire the real BackupService with controlled Moq boundaries.
/// Each test controls exactly what IHassioClient and IOneDriveClient return, then
/// asserts on the logic that BackupService adds on top — not on the mocks themselves.
/// </summary>
public class BackupApiIntegrationTests
{
    private readonly string _tempDataDir;

    public BackupApiIntegrationTests()
    {
        _tempDataDir = Path.Combine(Path.GetTempPath(), $"hassio_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDataDir);
    }

    private HttpClient CreateClient(Mock<IHassioClient> hassio, Mock<IOneDriveClient> oneDrive)
    {
        var tempDir = _tempDataDir;
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => { });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHassioClient>();
                    services.RemoveAll<IOneDriveClient>();
                    services.RemoveAll<ISettingsService>();
                    services.RemoveAll<IBackupService>();
                    services.RemoveAll<IHostedService>();

                    // Point RetentionDataStore at a temp dir so tests don't share state
                    services.AddSingleton<IConfiguration>(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["DataFolder"] = tempDir,
                                ["ConfigFolder"] = tempDir,
                                ["OneDrive:ClientId"] = "test-client-id",
                                ["GrafanaTelemetry:ServiceVersion"] = "9.9.9",
                                ["GrafanaTelemetry:ServiceBranch"] = "test",
                                ["DevelopmentMode"] = "false",
                            })
                            .Build());

                    services.AddSingleton(hassio.Object);
                    services.AddSingleton(oneDrive.Object);
                    services.AddSingleton<ISettingsService, MockSettingsService>();
                    services.AddSingleton<IBackupService>(sp =>
                        new BackupService(
                            sp.GetRequiredService<IHassioClient>(),
                            sp.GetRequiredService<IOneDriveClient>(),
                            sp.GetRequiredService<ISettingsService>(),
                            sp.GetRequiredService<IDateTimeProvider>(),
                            sp.GetRequiredService<RetentionDataStore>(),
                            sp.GetRequiredService<ILogger<BackupService>>()));
                });
            })
            .CreateClient();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Mock<IHassioClient> HassioWith(params Backup[] localBackups)
    {
        var mock = new Mock<IHassioClient>();
        mock.Setup(h => h.GetBackupsAsync(It.IsAny<Predicate<Backup>>()))
            .ReturnsAsync((Predicate<Backup> f) => localBackups.Where(b => f(b)).ToList());
        mock.Setup(h => h.GetTimeZoneAsync()).ReturnsAsync("UTC");
        mock.Setup(h => h.GetBackupInfoAsync(It.IsAny<string>())).ReturnsAsync((HassBackupInfoResponse?)null);
        return mock;
    }

    private static Mock<IOneDriveClient> OneDriveWith(params string[] slugsOnCloud)
    {
        var mock = new Mock<IOneDriveClient>();
        var items = slugsOnCloud
            .Select(slug => new DriveItem { Name = $"{slug}.tar", Size = 1024 })
            .ToList();
        mock.Setup(o => o.ListFilesInDirectoryAsync(It.IsAny<string>()))
            .ReturnsAsync(items);
        return mock;
    }

    private static Backup LocalBackup(string slug, string name, DateTime? date = null) => new()
    {
        Slug = slug,
        Name = name,
        Date = date ?? new DateTime(2024, 6, 1),
        Size = "1 GB",
        BackupType = "Full",
        SourceType = "Automated",
    };

    // -------------------------------------------------------------------------
    // GET /api/backup — status derivation logic
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBackups_LocalOnly_StatusIsLocal()
    {
        var hassio = HassioWith(LocalBackup("abc123", "My Backup"));
        var oneDrive = OneDriveWith(); // nothing on cloud
        var client = CreateClient(hassio, oneDrive);

        var backups = await (await client.GetAsync("/api/backup"))
            .Content.ReadFromJsonAsync<List<Backup>>();

        Assert.NotNull(backups);
        var b = Assert.Single(backups!);
        Assert.Equal("Local", b.Status);
    }

    [Fact]
    public async Task GetBackups_ExistsLocallyAndOnCloud_StatusIsSynced()
    {
        var hassio = HassioWith(LocalBackup("abc123", "My Backup"));
        var oneDrive = OneDriveWith("abc123"); // same slug on cloud
        var client = CreateClient(hassio, oneDrive);

        var backups = await (await client.GetAsync("/api/backup"))
            .Content.ReadFromJsonAsync<List<Backup>>();

        Assert.NotNull(backups);
        var b = Assert.Single(backups!);
        Assert.Equal("Synced", b.Status);
    }

    [Fact]
    public async Task GetBackups_OnCloudOnly_StatusIsOneDriveAndMerged()
    {
        var hassio = HassioWith(); // no local backups
        var oneDrive = OneDriveWith("cloud_only_slug");
        var client = CreateClient(hassio, oneDrive);

        var backups = await (await client.GetAsync("/api/backup"))
            .Content.ReadFromJsonAsync<List<Backup>>();

        Assert.NotNull(backups);
        var b = Assert.Single(backups!);
        Assert.Equal("OneDrive", b.Status);
        Assert.Equal("cloud_only_slug", b.Slug);
    }

    [Fact]
    public async Task GetBackups_MixedSources_MergedAndOrdered()
    {
        var hassio = HassioWith(
            LocalBackup("local_only", "Local Only", new DateTime(2024, 5, 1)),
            LocalBackup("synced_one", "Synced",     new DateTime(2024, 6, 1))
        );
        var oneDrive = OneDriveWith("synced_one", "cloud_only");
        var client = CreateClient(hassio, oneDrive);

        var backups = await (await client.GetAsync("/api/backup"))
            .Content.ReadFromJsonAsync<List<Backup>>();

        Assert.NotNull(backups);
        Assert.Equal(3, backups!.Count);

        // Ordered by date descending; cloud_only has no metadata → DateTime.MinValue → sorts last
        Assert.Equal("synced_one",  backups[0].Slug);
        Assert.Equal("local_only",  backups[1].Slug);
        Assert.Equal("cloud_only",  backups[2].Slug);

        Assert.Equal("Synced",    backups.First(b => b.Slug == "synced_one").Status);
        Assert.Equal("Local",     backups.First(b => b.Slug == "local_only").Status);
        Assert.Equal("OneDrive",  backups.First(b => b.Slug == "cloud_only").Status);
    }

    [Fact]
    public async Task GetBackups_RetainedSlug_RetainedFlagSet()
    {
        var hassio = HassioWith(LocalBackup("kept_slug", "Kept"));
        var oneDrive = OneDriveWith();
        var client = CreateClient(hassio, oneDrive);

        // Pin the backup first
        var retainPayload = JsonContent.Create(new { retain = true });
        var pinResponse = await client.PostAsync("/api/backup/kept_slug/retention", retainPayload);
        Assert.Equal(HttpStatusCode.OK, pinResponse.StatusCode);

        // Now list backups — retained flag should come from RetentionDataStore
        var backups = await (await client.GetAsync("/api/backup"))
            .Content.ReadFromJsonAsync<List<Backup>>();

        Assert.NotNull(backups);
        Assert.True(backups!.Single(b => b.Slug == "kept_slug").Retained);
    }

    // -------------------------------------------------------------------------
    // POST /api/backup/{slug}/upload — duplicate guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadBackup_FirstRequest_ReturnsOperationId()
    {
        var hassio = HassioWith(LocalBackup("up_slug", "To Upload"));
        // DownloadBackupAsync is called by the background task — stub it to avoid errors
        hassio.Setup(h => h.DownloadBackupAsync("up_slug"))
            .ReturnsAsync(LocalBackup("up_slug", "To Upload"));

        var oneDrive = OneDriveWith();
        oneDrive.Setup(o => o.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgressCallback>(), It.IsAny<string>()))
            .ReturnsAsync(new DriveItem());
        hassio.Setup(h => h.PublishEventAsync(It.IsAny<OneDriveEvents>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var client = CreateClient(hassio, oneDrive);

        var response = await client.PostAsync("/api/backup/up_slug/upload", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("operationId"));
        Assert.False(string.IsNullOrEmpty(result["operationId"]));
    }

    [Fact]
    public async Task UploadBackup_DuplicateWhileInProgress_Returns500()
    {
        var hassio = HassioWith(LocalBackup("dup_slug", "Dup"));
        // Block the background task so the operation stays InProgress
        hassio.Setup(h => h.DownloadBackupAsync("dup_slug"))
            .Returns(async () => { await Task.Delay(30_000); return LocalBackup("dup_slug", "Dup"); });
        hassio.Setup(h => h.PublishEventAsync(It.IsAny<OneDriveEvents>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var oneDrive = OneDriveWith();
        var client = CreateClient(hassio, oneDrive);

        // First request starts the operation
        var first = await client.PostAsync("/api/backup/dup_slug/upload", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second request while first is still InProgress should be rejected with 409 Conflict
        var second = await client.PostAsync("/api/backup/dup_slug/upload", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/backup/{slug}/info — local vs OneDrive-only
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBackupInfo_LocalBackup_ReturnsFullDetails()
    {
        var hassio = HassioWith(LocalBackup("info_slug", "Info Backup"));
        hassio.Setup(h => h.GetBackupInfoAsync("info_slug"))
            .ReturnsAsync(new HassBackupInfoResponse
            {
                Result = "ok",
                Data = new HassBackupInfoResponse.BackupInfoData
                {
                    Slug = "info_slug",
                    Name = "Info Backup",
                    Date = new DateTime(2024, 6, 1),
                    Size = 512.0f,
                    Type = "full",
                    Compressed = true,
                    Protected = false,
                    SupervisorVersion = "2024.6.0",
                    HomeAssistantVersion = "2024.6.1",
                    Addons = new List<HassBackupInfoResponse.BackupAddonInfoData>
                    {
                        new() { Slug = "core_ssh", Name = "SSH", Version = "1.0", Size = 10.0f }
                    },
                    Folders = new List<string> { "ssl" }
                }
            });

        var client = CreateClient(hassio, OneDriveWith());

        var response = await client.GetAsync("/api/backup/info_slug/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var info = await response.Content.ReadFromJsonAsync<BackupInfoResult>();
        Assert.NotNull(info);
        Assert.True(info!.IsAvailableLocally);
        Assert.Equal("info_slug", info.Slug);
        Assert.Equal("Info Backup", info.Name);
        Assert.True(info.Compressed);
        Assert.NotNull(info.Addons);
        Assert.Single(info.Addons!);
        Assert.Equal("core_ssh", info.Addons![0].Slug);
        Assert.NotNull(info.Folders);
        Assert.Contains("ssl", info.Folders!);
    }

    [Fact]
    public async Task GetBackupInfo_SlugNotInHass_ReturnsNotAvailableLocally()
    {
        var hassio = HassioWith(); // no local backups
        hassio.Setup(h => h.GetBackupInfoAsync(It.IsAny<string>()))
            .ReturnsAsync((HassBackupInfoResponse?)null);
        var client = CreateClient(hassio, OneDriveWith());

        var response = await client.GetAsync("/api/backup/ghost_slug/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var info = await response.Content.ReadFromJsonAsync<BackupInfoResult>();
        Assert.NotNull(info);
        Assert.False(info!.IsAvailableLocally);
        Assert.Equal("ghost_slug", info.Slug);
    }

    // -------------------------------------------------------------------------
    // GET /api/backup/sync-status — initial state
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSyncStatus_InitialState_IdleAndNotSyncing()
    {
        var client = CreateClient(HassioWith(), OneDriveWith());

        var response = await client.GetAsync("/api/backup/sync-status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isSyncing").GetBoolean());
        Assert.Equal("Idle", body.GetProperty("fileSyncState").GetProperty("state").GetString());
        // Null fields are omitted by WhenWritingNull — absence means no active operation
        Assert.False(body.TryGetProperty("activeUpload", out _));
        Assert.False(body.TryGetProperty("activeDownload", out _));
        Assert.False(body.TryGetProperty("activeBackupCreation", out _));
    }

    // -------------------------------------------------------------------------
    // GET /api/settings/build-info — reads from injected configuration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBuildInfo_ReturnsInjectedVersionAndBranch()
    {
        var client = CreateClient(HassioWith(), OneDriveWith());

        var response = await client.GetAsync("/api/settings/build-info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("9.9.9", body.GetProperty("version").GetString());
        Assert.Equal("test",  body.GetProperty("branch").GetString());
    }

    // -------------------------------------------------------------------------
    // PUT /api/settings — round-trip through real MockSettingsService
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateSettings_InstanceNamePersists()
    {
        var client = CreateClient(HassioWith(), OneDriveWith());

        var current = await (await client.GetAsync("/api/settings"))
            .Content.ReadFromJsonAsync<Settings>();
        Assert.NotNull(current);

        current!.General.InstanceName = "my-ha-instance";
        var putResponse = await client.PutAsJsonAsync("/api/settings", current);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updated = await (await client.GetAsync("/api/settings"))
            .Content.ReadFromJsonAsync<Settings>();
        Assert.Equal("my-ha-instance", updated!.General.InstanceName);
    }
}
