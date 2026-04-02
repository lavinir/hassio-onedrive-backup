using System.Net;
using System.Net.Http.Json;
using HassioOneDriveBackup.Models;
using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Services.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace HassioOneDriveBackup.Tests;

public class BackupApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BackupApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Replace real clients with mocks / in-memory services
                services.RemoveAll<IHassioClient>();
                services.RemoveAll<IOneDriveClient>();
                services.RemoveAll<ISettingsService>();
                services.RemoveAll<IBackupService>();
                services.RemoveAll<IHostedService>();   // no background services during tests

                var mockOneDrive = new Mock<IOneDriveClient>();
                mockOneDrive.Setup(o => o.IsLoggedInAsync())
                    .ReturnsAsync(new OneDriveAuthInfo { AuthState = OneDriveAuthState.LoggedIn });
                mockOneDrive.Setup(o => o.ListFilesInDirectoryAsync(It.IsAny<string>()))
                    .ReturnsAsync(new List<Microsoft.Graph.Models.DriveItem>());

                services.AddSingleton<IHassioClient>(sp =>
                    new MockHassioClient(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MockHassioClient>>()));
                services.AddSingleton<IOneDriveClient>(mockOneDrive.Object);
                services.AddSingleton<ISettingsService, MockSettingsService>();
                services.AddSingleton<IBackupService>(sp =>
                    new BackupService(
                        sp.GetRequiredService<IHassioClient>(),
                        sp.GetRequiredService<IOneDriveClient>(),
                        sp.GetRequiredService<ISettingsService>(),
                        sp.GetRequiredService<IDateTimeProvider>(),
                        sp.GetRequiredService<RetentionDataStore>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackupService>>()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetBackups_ReturnsOkWithList()
    {
        var response = await _client.GetAsync("/api/backup");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var backups = await response.Content.ReadFromJsonAsync<List<Backup>>();
        Assert.NotNull(backups);
        Assert.NotEmpty(backups);
    }

    [Fact]
    public async Task UploadBackup_ReturnsOperationId()
    {
        var response = await _client.PostAsync("/api/backup/mock_backup_full_20240101/upload", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("operationId"));
        Assert.False(string.IsNullOrEmpty(result["operationId"]));
    }

    [Fact]
    public async Task GetSettings_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadFromJsonAsync<Settings>();
        Assert.NotNull(settings);
    }

    [Fact]
    public async Task UpdateSettings_PersistsAndReturns()
    {
        // Get current settings
        var current = await (await _client.GetAsync("/api/settings"))
            .Content.ReadFromJsonAsync<Settings>();
        Assert.NotNull(current);

        // Mutate and PUT
        current!.General.InstanceName = "test-instance";
        var putResponse = await _client.PutAsJsonAsync("/api/settings", current);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        // GET again — should reflect the change
        var updated = await (await _client.GetAsync("/api/settings"))
            .Content.ReadFromJsonAsync<Settings>();
        Assert.Equal("test-instance", updated!.General.InstanceName);
    }
}
