using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Hass;
using Moq;
using onedrive_backup.Graph;
using onedrive_backup.Hass;
using onedrive_backup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using test.onedrive_backup.Mocks;
using hassio_onedrive_backup.Graph;
using Newtonsoft.Json;
using hassio_onedrive_backup;
using hassio_onedrive_backup.Sync;

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
		private MockDateTimeProvider _mockDateTimeProvider;

		[TestInitialize]
		public void Setup()
		{
			_serviceProviderMock = new Mock<IServiceProvider>();

			SetupIGraphHelper();
			_hassIoClientMock = new Mock<IHassioClient>();

			_hassEntityStateMock = new Mock<HassOnedriveFileSyncEntityState>(_hassIoClientMock.Object);
			_transferSpeedHelperMock = new Mock<TransferSpeedHelper>(null);
			_addonOptions = CreateAddonOptions();
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IGraphHelper))).Returns(_graphHelperMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IHassioClient))).Returns(_hassIoClientMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(HassOnedriveEntityState))).Returns(_hassEntityStateMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(AddonOptions))).Returns(_addonOptions);
			_mockDateTimeProvider = new MockDateTimeProvider();
			var consoleLogger = new ConsoleLogger();
			consoleLogger.SetDateTimeProvider(_mockDateTimeProvider);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(ConsoleLogger))).Returns(consoleLogger);

			_syncManager = new Mock<SyncManager>()(
				_serviceProviderMock.Object,
				_addonOptions.BackupAllowedHours,
				_transferSpeedHelperMock.Object);
		}

		private void SetupIGraphHelper()
		{
			_graphHelperMock = new Mock<IGraphHelper>();
			_graphHelperMock.Setup(gh => gh.GetItemsInAppFolderAsync(""))
				.ReturnsAsync(() => _onedriveBackups.Select(backup => new Microsoft.Graph.DriveItem()
				{
					Name = backup.FileName,
					Description = JsonConvert.SerializeObject(new OnedriveItemDescription
					{
						Addons = backup.Addons,
						BackupDate = backup.BackupDate,
						BackupType = backup.Type,
						Folders = backup.Folders,
						InstanceName = backup.InstanceName,
						IsProtected = backup.IsProtected,
						Slug = backup.Slug,
						Size = backup.Size
					})
				}).ToList());


			_graphHelperMock.Setup(gh => gh.UploadFileAsync(
				It.IsAny<string>(),
				It.IsAny<DateTime>(),
				It.IsAny<string>(),
				It.IsAny<TransferSpeedHelper>(),
				It.IsAny<string>(),
				It.IsAny<Action<int, int>?>(),
				It.IsAny<bool>(),
				It.IsAny<string>())).Callback((string slug, DateTime date, string instanceName, TransferSpeedHelper _, string _, Action<int, int>? _, bool _, string _) => _onedriveBackups.Add(new OnedriveBackup
				{
					FileName = slug,
					InstanceName = instanceName,
					Slug = slug,
					BackupDate = date
				}));

			_graphHelperMock
				.Setup(gh => gh.DeleteItemFromAppFolderAsync(It.IsAny<string>()))
				.ReturnsAsync((string fileName) =>
				{
					int deleted = _onedriveBackups.RemoveAll(backup => backup.FileName.Equals(fileName));
					return deleted == 1;
				});
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
