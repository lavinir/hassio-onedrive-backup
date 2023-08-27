using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using hassio_onedrive_backup;
using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using onedrive_backup.Graph;
using onedrive_backup.Hass;
using test.onedrive_backup.Mocks;
using YourNamespace; // Import the appropriate namespace
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace YourNamespace.Tests
{
	[TestClass]
	public class BackupManagerTests
	{
		private Mock<IServiceProvider> _serviceProviderMock;
		private Mock<IGraphHelper> _graphHelperMock;
		private Mock<IHassioClient> _hassIoClientMock;
		private Mock<HassOnedriveEntityState> _hassEntityStateMock;
		private Mock<TransferSpeedHelper> _transferSpeedHelperMock;
		private Mock<HassContext> _hassContextMock;
		private AddonOptions _addonOptions;
		private BackupManagerMock _backupManager;
		private List<Backup> _localBackups = new List<Backup>();
		private List<OnedriveBackup> _onedriveBackups = new List<OnedriveBackup>();

		[TestInitialize]
		public void Setup()
		{
			DateTimeHelper.Initialize("Local");
			_serviceProviderMock = new Mock<IServiceProvider>();
			
			_graphHelperMock = new Mock<IGraphHelper>();
			_graphHelperMock.Setup(gh => gh.GetItemsInAppFolderAsync(""))
				.Returns(Task.FromResult(_onedriveBackups.Select(backup => new Microsoft.Graph.DriveItem()
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
				}).ToList()));

			_hassIoClientMock = new Mock<IHassioClient>();
			_hassIoClientMock
				.Setup(hassIoClient => hassIoClient.CreateBackupAsync(It.IsAny<string>(), It.IsAny<bool>(), true, It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
				.Returns((string backupName, bool appendTS, bool compressed, string password, IEnumerable<string> folders, IEnumerable<string> addons) =>
					{
						_localBackups.Add(new Backup()
						{
							Date = DateTimeHelper.Instance.Now,
							Slug = Guid.NewGuid().ToString(),
							Protected = string.IsNullOrEmpty(password) ? false : true,
							Compressed = compressed,					
							Name = backupName
						});

						return Task.FromResult(true);
					});
			_hassEntityStateMock = new Mock<HassOnedriveEntityState>(_hassIoClientMock.Object);
			_transferSpeedHelperMock = new Mock<TransferSpeedHelper>(null);
			_hassContextMock = new Mock<HassContext>();
			_addonOptions = CreateAddonOptions();
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IGraphHelper))).Returns(_graphHelperMock.Object); 
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IHassioClient))).Returns(_hassIoClientMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(HassOnedriveEntityState))).Returns(_hassEntityStateMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(HassContext))).Returns(_hassContextMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(AddonOptions))).Returns(_addonOptions);

			_backupManager = new BackupManagerMock(
				_serviceProviderMock.Object,
				_transferSpeedHelperMock.Object);
		}

		[TestMethod]
		public async Task PerformBackupsAsync_WhenIsExecuting_ShouldSkipBackup()
		{
			_backupManager.SetIsExecuting(true);

			await _backupManager.PerformBackupsAsync();

			_hassIoClientMock.Verify(client => client.GetBackupsAsync(It.IsAny<Predicate<Backup>>()), Times.Never());
		}

		[TestMethod]
		public async Task PerformBackupsAsync_WhenConditionsMet_ShouldCreateLocalBackupAndUpload()
		{			
			await _backupManager.PerformBackupsAsync();
			Assert.IsTrue(_localBackups.Count == 1);
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
