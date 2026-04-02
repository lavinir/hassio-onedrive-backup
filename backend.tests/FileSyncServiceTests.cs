using System.Security.Cryptography;
using HassioOneDriveBackup.Models;
using HassioOneDriveBackup.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.Graph.Models;

namespace HassioOneDriveBackup.Tests;

public class FileSyncServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IOneDriveClient> _oneDrive;
    private readonly Mock<ISettingsService> _settings;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IHassioClient> _hassio;
    private readonly HassEntityStateService _entityState;
    private readonly FileSyncService _svc;

    public FileSyncServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"filesynctest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _oneDrive = new Mock<IOneDriveClient>();
        _settings = new Mock<ISettingsService>();
        _dateTimeProvider = new Mock<IDateTimeProvider>();
        _hassio = new Mock<IHassioClient>();
        _entityState = new HassEntityStateService(_hassio.Object, NullLogger<HassEntityStateService>.Instance);

        _hassio.Setup(h => h.UpdateHassEntityStateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _dateTimeProvider.Setup(d => d.Now).Returns(new DateTime(2024, 6, 10, 12, 0, 0));

        _svc = new FileSyncService(
            _oneDrive.Object,
            _settings.Object,
            _dateTimeProvider.Object,
            _entityState,
            NullLogger<FileSyncService>.Instance);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private Settings MakeSyncSettings(string glob, bool removeDeleted = false)
    {
        return new Settings
        {
            Backup = new BackupSettings { BackupAllowedHours = "*" },
            FileSync = new FileSyncSettings
            {
                SyncPaths = new List<string> { glob },
                FileSyncRemoveDeleted = removeDeleted,
                IgnoreAllowedHoursForFileSync = true
            }
        };
    }

    private string CreateFile(string name, string content = "hello world")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DriveItem MakeDriveItem(string localPath, bool matchHash = true)
    {
        var fileInfo = new FileInfo(localPath);
        string hash;
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(localPath))
            hash = matchHash
                ? BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "")
                : "DEADBEEF";

        return new DriveItem
        {
            Size = matchHash ? fileInfo.Length : 999,
            File = new FileObject
            {
                Hashes = new Hashes { Sha256Hash = hash }
            }
        };
    }

    [Fact]
    public async Task HashMatch_FileNotUploaded()
    {
        var filePath = CreateFile("test.txt");
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(MakeSyncSettings($"{_tempDir}/*.txt"));

        var remote = MakeDriveItem(filePath, matchHash: true);
        _oneDrive.Setup(o => o.GetFileAsync(It.IsAny<string>())).ReturnsAsync(remote);

        await InvokeSyncTick(CancellationToken.None);

        _oneDrive.Verify(o => o.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgressCallback>()), Times.Never);
    }

    [Fact]
    public async Task HashMismatch_FileUploaded()
    {
        var filePath = CreateFile("test.txt");
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(MakeSyncSettings($"{_tempDir}/*.txt"));

        var remote = MakeDriveItem(filePath, matchHash: false);
        _oneDrive.Setup(o => o.GetFileAsync(It.IsAny<string>())).ReturnsAsync(remote);
        _oneDrive.Setup(o => o.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgressCallback>()))
            .ReturnsAsync(new DriveItem());

        await InvokeSyncTick(CancellationToken.None);

        _oneDrive.Verify(o => o.UploadFileAsync(filePath, It.IsAny<string>(), It.IsAny<ProgressCallback>()), Times.Once);
    }

    [Fact]
    public async Task NotOnRemote_FileUploaded()
    {
        var filePath = CreateFile("test.txt");
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(MakeSyncSettings($"{_tempDir}/*.txt"));

        _oneDrive.Setup(o => o.GetFileAsync(It.IsAny<string>())).ReturnsAsync((DriveItem?)null);
        _oneDrive.Setup(o => o.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgressCallback>()))
            .ReturnsAsync(new DriveItem());

        await InvokeSyncTick(CancellationToken.None);

        _oneDrive.Verify(o => o.UploadFileAsync(filePath, It.IsAny<string>(), It.IsAny<ProgressCallback>()), Times.Once);
    }

    [Fact]
    public async Task ZeroByteFile_Skipped()
    {
        var filePath = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllBytes(filePath, Array.Empty<byte>());
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(MakeSyncSettings($"{_tempDir}/*.txt"));

        _oneDrive.Setup(o => o.GetFileAsync(It.IsAny<string>())).ReturnsAsync((DriveItem?)null);

        await InvokeSyncTick(CancellationToken.None);

        _oneDrive.Verify(o => o.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgressCallback>()), Times.Never);
    }

    [Fact]
    public async Task NoSyncPaths_TickSkipped()
    {
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new Settings
        {
            Backup = new BackupSettings { BackupAllowedHours = "*" },
            FileSync = new FileSyncSettings
            {
                SyncPaths = new List<string>(),
                IgnoreAllowedHoursForFileSync = true
            }
        });

        await InvokeSyncTick(CancellationToken.None);

        _oneDrive.Verify(o => o.GetFileAsync(It.IsAny<string>()), Times.Never);
        _oneDrive.Verify(o => o.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgressCallback>()), Times.Never);
    }

    [Fact]
    public async Task FileSyncRemoveDeleted_RemoteOrphanDeleted()
    {
        // No local files match the glob, but a remote file exists
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(MakeSyncSettings($"{_tempDir}/*.txt", removeDeleted: true));

        // Remote has a file that doesn't exist locally
        var remoteFile = new DriveItem
        {
            Name = "ghost.txt",
            File = new FileObject(),
            Size = 100
        };
        _oneDrive.Setup(o => o.ListFilesInDirectoryAsync("FileSync"))
            .ReturnsAsync(new List<DriveItem> { remoteFile });
        _oneDrive.Setup(o => o.ListFilesInDirectoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<DriveItem>());
        _oneDrive.Setup(o => o.ListFilesInDirectoryAsync("FileSync"))
            .ReturnsAsync(new List<DriveItem> { remoteFile });
        _oneDrive.Setup(o => o.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await InvokeSyncTick(CancellationToken.None);

        _oneDrive.Verify(o => o.DeleteFileAsync(It.Is<string>(p => p.Contains("ghost.txt"))), Times.Once);
    }

    // Invoke the protected RunFileSyncTickAsync via ExecuteAsync with a quick cancel
    private async Task InvokeSyncTick(CancellationToken ct)
    {
        using var cts = new CancellationTokenSource();
        // Use reflection to call the internal tick method directly
        var method = typeof(FileSyncService).GetMethod("RunFileSyncTickAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null) throw new InvalidOperationException("RunFileSyncTickAsync not found");
        await (Task)method.Invoke(_svc, new object[] { ct })!;
    }
}
