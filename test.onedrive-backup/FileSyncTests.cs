using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Hass;
using Moq;
using onedrive_backup.Graph;
using test.onedrive_backup.Mocks;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup;
using hassio_onedrive_backup.Sync;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using onedrive_backup.Sync;
using Microsoft.Graph.Models;

namespace test.onedrive_backup
{
    [TestClass]
	public class FileSyncTests
	{
		private Mock<IServiceProvider> _serviceProviderMock;
		private Mock<IGraphHelper> _graphHelperMock;
		private Mock<IHassioClient> _hassIoClientMock;
		private Mock<HassOnedriveFileSyncEntityState> _hassEntityStateMock;
		private Mock<TransferSpeedHelper> _transferSpeedHelperMock;
		private AddonOptions _addonOptions;
		private Mock<IWebHostEnvironment> _iWebHostEnvironmentMock;
		private MockDateTimeProvider _mockDateTimeProvider;
		private object _syncManagerMock;
		private List<DriveItem> _syncedFiles = new();

		[TestInitialize]
		public void Setup()
		{
			_syncedFiles = new();
			_serviceProviderMock = new Mock<IServiceProvider>();

			SetupIGraphHelper();
			_hassIoClientMock = new Mock<IHassioClient>();

			_hassEntityStateMock = new Mock<HassOnedriveFileSyncEntityState>(_hassIoClientMock.Object);
			_transferSpeedHelperMock = new Mock<TransferSpeedHelper>(null);
			_addonOptions = CreateAddonOptions();
			_iWebHostEnvironmentMock = new Mock<IWebHostEnvironment>();
			_iWebHostEnvironmentMock.Setup(env => env.EnvironmentName).Returns(Environments.Development);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IGraphHelper))).Returns(_graphHelperMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IHassioClient))).Returns(_hassIoClientMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(HassOnedriveEntityState))).Returns(_hassEntityStateMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(AddonOptions))).Returns(_addonOptions);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IWebHostEnvironment))).Returns(_iWebHostEnvironmentMock.Object);
			_mockDateTimeProvider = new MockDateTimeProvider();
			var consoleLogger = new ConsoleLogger();
			consoleLogger.SetDateTimeProvider(_mockDateTimeProvider);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(ConsoleLogger))).Returns(consoleLogger);

			_syncManagerMock = new Mock<SyncManager>(
				_serviceProviderMock.Object,
				TimeRangeHelper.GetAllowedHours(_addonOptions.BackupAllowedHours),
				_transferSpeedHelperMock.Object,
				consoleLogger,
				_mockDateTimeProvider);
		}

		private void SetupIGraphHelper()
		{
			_graphHelperMock = new Mock<IGraphHelper>();
			//_graphHelperMock.Setup(gh => gh.GetItemsInAppFolderAsync(""))
			//	.ReturnsAsync(() => _onedriveBackups.Select(backup => new Microsoft.Graph.DriveItem()
			//	{
			//		Name = backup.FileName,
			//		Description = JsonConvert.SerializeObject(new OnedriveItemDescription
			//		{
			//			Addons = backup.Addons,
			//			BackupDate = backup.BackupDate,
			//			BackupType = backup.Type,
			//			Folders = backup.Folders,
			//			InstanceName = backup.InstanceName,
			//			IsProtected = backup.IsProtected,
			//			Slug = backup.Slug,
			//			Size = backup.Size
			//		})
			//	}).ToList());


			_graphHelperMock.Setup(gh => gh.UploadFileAsync(
				It.IsAny<string>(),
				It.IsAny<DateTime>(),
				It.IsAny<string>(),
				It.IsAny<TransferSpeedHelper>(),
				It.IsAny<string>(),
				It.IsAny<Action<int, int>?>(),
				It.IsAny<bool>(),
				It.IsAny<string>())).Callback((string filePath, DateTime date, string instanceName, TransferSpeedHelper _, string remotePath, Action<int, int>? _, bool _, string _) => 
				{
					var fileHash = FileOperationHelper.CalculateFileHash(filePath);
					var fileInfo = new FileInfo(filePath);
					string[] pathComponents = filePath.Split(Path.DirectorySeparatorChar);

					_syncedFiles.Add(new DriveItem
					{
						Name = fileInfo.Name,
						AdditionalData = new Dictionary<string, object>()
						{
							{"remotePath",  GetRemotePath(filePath)}
						},
						Size = fileInfo.Length,
						File = new FileObject()
						{
							Hashes = new Hashes
							{
								Sha256Hash = fileHash
							},
						}
					});
				}
				);

			_graphHelperMock
				.Setup(gh => gh.DeleteItemFromAppFolderAsync(It.IsAny<string>()))
				.ReturnsAsync((string remotePath) =>
				{
					int deleted = _syncedFiles.RemoveAll(item => item.AdditionalData["remotePath"].ToString().Equals(remotePath));
					return deleted == 1;
				});
		}

		private string GetRemotePath(string filePath)
		{
			string remotePath = $"/{SyncManager.OneDriveFileSyncRootDir}{filePath}".Replace("//", "/").Replace(@"\\", @"\");
			return remotePath;
		}

		private AddonOptions CreateAddonOptions()
		{
			return new AddonOptions
			{
				BackupIntervalDays = 1,
				MaxLocalBackups = 2,
				MaxOnedriveBackups = 3
			};
		}
	}
}
