using hassio_onedrive_backup;
using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Microsoft.Graph;
using Moq;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace tests
{
    [TestClass]
    public class BackupTests
    {
        private List<Backup> _localBackups = new List<Backup>();
        private List<OnedriveBackup> _onedriveBackups = new List<OnedriveBackup>();
        private List<string> _syncedFiles = new List<string>();
        private List<DriveItem> _onedriveItems = new List<DriveItem>();

        Dictionary<string, Backup> _tmpBackupFiles = new Dictionary<string, Backup>();
        private Mock<IHassioClient> _hassioClientMock;
        private Mock<IGraphHelper> _graphHelperMock;
        private AddonOptions _addonOptions;

        public BackupTests()
        {
            _hassioClientMock = CreatHassIoMock();
            _graphHelperMock = CreateGraphHelperMock();
        }

        [TestInitialize]
        public void ResetState()
        {
            _localBackups.Clear();
            _onedriveBackups.Clear();
            _tmpBackupFiles.Clear();
            _syncedFiles.Clear();

            _addonOptions = new AddonOptions
            {
                MaxLocalBackups = 1,
                MaxOnedriveBackups = 3,
                BackupIntervalDays = 1,
                BackupName = "backups",
                RecoveryMode = false
            };

        }

        [TestMethod]
        public async Task TestBackupCreatedWhenStale()
        {
            Orchestrator orchestrator = new Orchestrator(_hassioClientMock.Object, _graphHelperMock.Object, _addonOptions);
            orchestrator.Start();
            await Task.Delay(10000);
            orchestrator.Stop();
            Assert.IsTrue(_localBackups.Count == 1);
            Assert.IsTrue(_onedriveBackups.Count == 1);
        }

        [TestMethod]
        public void TestBackupNotCreatedWhenFresh()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestOneDriveMaxBackupLimit()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestLocalMaxBackupLimit()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void RunRecoveryModeWhenSelected()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestFileSyncDirectoryNoDelete()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestFileSyncDirectoryDelete()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestFileSyncWildCard()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestAllowedHoursBackup()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestAllowedHoursFileSync()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestOverLappingIterations()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestBackupSensorValues()
        {
            Assert.Fail();
        }


        [TestMethod]
        public void TestFileSyncSensorValues()
        {
            Assert.Fail();
        }

        private Mock<IHassioClient> CreatHassIoMock()
        {
            var hassioClientMock = new Mock<IHassioClient>();
            hassioClientMock
                .Setup(client => client.CreateBackupAsync(It.IsAny<string>(), It.IsAny<bool>(), true, It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>()))
                .ReturnsAsync((string backupName, bool appendTimeStamp, bool compressed, string password, IEnumerable<string> folders, IEnumerable<string> addons) =>
                {
                    var backup = new Backup
                    {
                        Slug = Guid.NewGuid().ToString(),
                        Date = DateTimeHelper.Instance.Now,
                        Name = backupName,
                        Type = folders == null && addons == null ? "full" : "partial",
                        Size = 1000,
                        Protected = password != null,
                        Compressed = true,
                        Content = new Content
                        {
                            Homeassistant = true,
                            Addons = addons?.ToArray(),
                            Folders = folders?.ToArray()
                        }
                    };
                    return true;
                });
            
            hassioClientMock
                .Setup(client => client.UploadBackupAsync(It.IsAny<string>()))
                .ReturnsAsync((string path) =>
                {
                    _localBackups.Add(_tmpBackupFiles[path]);
                    return true;         
                });

            hassioClientMock
                .Setup(client => client.DownloadBackupAsync(It.IsAny<string>()))
                .ReturnsAsync((string slug) =>
                {
                    // fake path
                    return Guid.NewGuid().ToString();
                });

            hassioClientMock
                .Setup(client => client.DeleteBackupAsync(It.IsAny<Backup>()))
                .ReturnsAsync((Backup backup) =>
                {
                    _localBackups.Remove(backup);
                    return true;
                });

            return hassioClientMock;
        }

        private Mock<IGraphHelper> CreateGraphHelperMock()
        {
            var mock = new Mock<IGraphHelper>();
            mock
                .Setup(graphHelper => graphHelper.UploadFileAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<int>>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((string path, DateTime dt, string instanceName, string destFileName, Action<int> progressCallback, bool flatten, bool omitDescription) =>
                {
                    if (omitDescription == true)
                    {
                        // File Sync
                        _syncedFiles.Add(path);
                    }
                    else
                    {  
                        // Backup

                        _onedriveBackups.Add(new OnedriveBackup(destFileName ?? Guid.NewGuid().ToString(), new OnedriveItemDescription { BackupDate = dt, InstanceName = instanceName, Slug = Guid.NewGuid().ToString() }));
                    }
                    return true;
                });

            mock
                .Setup(graphHelper => graphHelper.DownloadFileAsync(It.IsAny<string>(), It.IsAny<Action<int?>>()))
                .ReturnsAsync((string fileName, Action<int?> progressCallback) =>
                {
                    string fakePath = Guid.NewGuid().ToString();
                    var onlineBackup = _onedriveBackups.First(bckup => bckup.FileName.Equals(fileName));
                    _tmpBackupFiles.Add(fakePath, new Backup { Name = fileName, Date = onlineBackup.BackupDate, Slug = onlineBackup.Slug });
                    return fakePath;
                });

            mock
                .Setup(graphHelper => graphHelper.GetItemInAppFolderAsync(It.IsAny<string>()))
                .ReturnsAsync((string filePath) =>
                {
                    return _syncedFiles.Select(filePath => new DriveItem { Name = Path.GetFileName(filePath), File = new Microsoft.Graph.File() }).ToList();
                });
            return mock;
        }

    }
}