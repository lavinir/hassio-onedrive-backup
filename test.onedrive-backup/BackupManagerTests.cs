using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Microsoft.Graph.Models;
using Moq;
using Newtonsoft.Json;
using onedrive_backup;
using onedrive_backup.Graph;
using onedrive_backup.Hass;
using System;
using System.Globalization;
using test.onedrive_backup.Mocks;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Tests
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
		private List<Backup> _localBackups = new();
		private List<OnedriveBackup> _onedriveBackups = new();
		private MockDateTimeProvider _dateTimeProvider = new MockDateTimeProvider();

		[TestInitialize]
		public void Setup()
		{
			_serviceProviderMock = new Mock<IServiceProvider>();

			SetupIGraphHelper();
			SetupHassIoClient();

			_localBackups.Clear();
			_onedriveBackups.Clear();

			_hassEntityStateMock = new Mock<HassOnedriveEntityState>(_hassIoClientMock.Object);
			_transferSpeedHelperMock = new Mock<TransferSpeedHelper>(null);
			_hassContextMock = new Mock<HassContext>();
			_addonOptions = CreateAddonOptions();
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IGraphHelper))).Returns(_graphHelperMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IHassioClient))).Returns(_hassIoClientMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(HassOnedriveEntityState))).Returns(_hassEntityStateMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(HassContext))).Returns(_hassContextMock.Object);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(AddonOptions))).Returns(_addonOptions);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(IDateTimeProvider))).Returns(_dateTimeProvider);

			var consoleLogger = new ConsoleLogger();
			consoleLogger.SetDateTimeProvider(_dateTimeProvider);
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(ConsoleLogger))).Returns(consoleLogger);

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
			Assert.IsTrue(_onedriveBackups.Count == 1);
			Assert.IsTrue(_localBackups.Single().Slug.Equals(_onedriveBackups.Single().Slug));
		}

		#region LocalBackupTests

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_Max4_Local()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxLocalBackups = 4;
			var testStartDate = _dateTimeProvider.Now;

			for (int day = 0; day < _addonOptions.MaxLocalBackups; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			Assert.IsTrue(_localBackups.Count == _addonOptions.MaxLocalBackups);
			for (int day = 0; day < _addonOptions.MaxLocalBackups; day++)
			{
				Assert.IsTrue(_localBackups.Single(backup => backup.BackupDate.Date.Equals(testStartDate.Date.AddDays(day))) != null);
			}
		}

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_Max2_Local()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxLocalBackups = 2;
			var testStartDate = _dateTimeProvider.Now;

			for (int day = 0; day < _addonOptions.MaxLocalBackups + 1; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			Assert.IsTrue(_localBackups.Count == _addonOptions.MaxLocalBackups);
			for (int day = 1; day <= _addonOptions.MaxLocalBackups; day++)
			{
				Assert.IsTrue(_localBackups.Single(backup => backup.BackupDate.Date.Equals(testStartDate.Date.AddDays(day))) != null);
			}
		}

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_2_Weeks_4_Months_Max10_Local()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;
			_addonOptions.MaxLocalBackups = 10;

			var testStartDate = _dateTimeProvider.Now;
			int testDays = 120;

			for (int day = 0; day < testDays; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			_dateTimeProvider.TimeSeek(-1);
			Assert.IsTrue(_localBackups.Count == _addonOptions.MaxLocalBackups);

			// Verify Months
			for (int i = 0; i < _addonOptions.GenerationalMonths; i++)
			{
				var month = _dateTimeProvider.Now.Month;
				month -= i;
				if (month < 1)
				{
					month = 12 - Math.Abs(month);
				}

				Assert.IsTrue(_localBackups.Any(backup => backup.Date.Month == month));
			}

			// Verify Weeks
			var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(_dateTimeProvider.Now, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday);
			for (int week = currentWeek; week > currentWeek - _addonOptions.GenerationalWeeks; week--)
			{
				Assert.IsTrue(_localBackups.Any(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday) == week));
			}

			for (int day = 0; day <= _addonOptions.GenerationalDays; day++)
			{
				Assert.IsTrue(_localBackups.Single(backup => backup.BackupDate.Date.Equals(_dateTimeProvider.Now.Date.AddDays(-day))) != null);
			}
		}

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_2_Weeks_4_Months_2_years_Max10_Local()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;
			_addonOptions.GenerationalYears = 2;
			_addonOptions.MaxLocalBackups = 10;

			var testStartDate = _dateTimeProvider.Now;
			int testDays = 1000;

			for (int day = 0; day < testDays; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			_dateTimeProvider.TimeSeek(-1);
			Assert.IsTrue(_localBackups.Count == _addonOptions.MaxLocalBackups);

			// Verify Years
			int currentYear = _dateTimeProvider.Now.Year;
			for (int i =0; i< _addonOptions.GenerationalYears; i++)
			{
				Assert.IsTrue(_localBackups.Any(backup => backup.Date.Year == currentYear - i));
			}

			// Verify Months
			for (int i = 0; i < _addonOptions.GenerationalMonths; i++)
			{
				var month = _dateTimeProvider.Now.Month;
				month -= i;
				if (month < 1)
				{
					month = 12 - Math.Abs(month);
				}

				Assert.IsTrue(_localBackups.Any(backup => backup.Date.Month == month));
			}

			// Verify Weeks
			var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(_dateTimeProvider.Now, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday);
			for (int week = currentWeek; week > currentWeek - _addonOptions.GenerationalWeeks; week--)
			{
				Assert.IsTrue(_localBackups.Any(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday) == week));
			}

			// Verify Days
			for (int day = 0; day <= _addonOptions.GenerationalDays; day++)
			{
				Assert.IsTrue(_localBackups.Single(backup => backup.BackupDate.Date.Equals(_dateTimeProvider.Now.Date.AddDays(-day))) != null);
			}
		}


		#endregion

		#region OnlineBackupTests

		public async Task Test_Generational_Retention_3_Days_Max4_Online()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxOnedriveBackups = 4;
			var testStartDate = _dateTimeProvider.Now;

			for (int day = 0; day < _addonOptions.MaxOnedriveBackups; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			Assert.IsTrue(_onedriveBackups.Count == _addonOptions.MaxOnedriveBackups);
			for (int day = 0; day < _addonOptions.MaxOnedriveBackups; day++)
			{
				Assert.IsTrue(_onedriveBackups.Single(backup => backup.BackupDate.Date.Equals(testStartDate.Date.AddDays(day))) != null);
			}
		}

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_Max2_Online()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxOnedriveBackups = 2;
			var testStartDate = _dateTimeProvider.Now;

			for (int day = 0; day < _addonOptions.MaxOnedriveBackups + 1; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			Assert.IsTrue(_onedriveBackups.Count == _addonOptions.MaxOnedriveBackups);
			for (int day = 1; day <= _addonOptions.MaxOnedriveBackups; day++)
			{
				Assert.IsTrue(_onedriveBackups.Single(backup => backup.BackupDate.Date.Equals(testStartDate.Date.AddDays(day))) != null);
			}
		}

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_2_Weeks_4_Months_Max10_Online()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;
			_addonOptions.MaxOnedriveBackups = 10;

			var testStartDate = _dateTimeProvider.Now;
			int testDays = 120;

			for (int day = 0; day < testDays; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			_dateTimeProvider.TimeSeek(-1);
			Assert.IsTrue(_onedriveBackups.Count == _addonOptions.MaxOnedriveBackups);

			// Verify Months
			for (int i = 0; i < _addonOptions.GenerationalMonths; i++)
			{
				var month = _dateTimeProvider.Now.Month;
				month -= i;
				if (month < 1)
				{
					month = 12 - Math.Abs(month);
				}

				Assert.IsTrue(_onedriveBackups.Any(backup => backup.BackupDate.Month == month));
			}

			// Verify Weeks
			var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(_dateTimeProvider.Now, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday);
			for (int week = currentWeek; week > currentWeek - _addonOptions.GenerationalWeeks; week--)
			{
				Assert.IsTrue(_onedriveBackups.Any(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday) == week));
			}

			for (int day = 0; day <= _addonOptions.GenerationalDays; day++)
			{
				Assert.IsTrue(_onedriveBackups.Single(backup => backup.BackupDate.Date.Equals(_dateTimeProvider.Now.Date.AddDays(-day))) != null);
			}
		}

		[TestMethod]
		public async Task Test_Generational_Retention_3_Days_2_Weeks_4_Months_2_years_Max10_Online()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;
			_addonOptions.GenerationalYears = 2;
			_addonOptions.MaxOnedriveBackups = 10;

			var testStartDate = _dateTimeProvider.Now;
			int testDays = 1000;

			for (int day = 0; day < testDays; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			_dateTimeProvider.TimeSeek(-1);
			Assert.IsTrue(_onedriveBackups.Count == _addonOptions.MaxOnedriveBackups);

			// Verify Years
			int currentYear = _dateTimeProvider.Now.Year;
			for (int i = 0; i < _addonOptions.GenerationalYears; i++)
			{
				Assert.IsTrue(_onedriveBackups.Any(backup => backup.BackupDate.Year == currentYear - i));
			}

			// Verify Months
			for (int i = 0; i < _addonOptions.GenerationalMonths; i++)
			{
				var month = _dateTimeProvider.Now.Month;
				month -= i;
				if (month < 1)
				{
					month = 12 - Math.Abs(month);
				}

				Assert.IsTrue(_onedriveBackups.Any(backup => backup.BackupDate.Month == month));
			}

			// Verify Weeks
			var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(_dateTimeProvider.Now, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday);
			for (int week = currentWeek; week > currentWeek - _addonOptions.GenerationalWeeks; week--)
			{
				Assert.IsTrue(_onedriveBackups.Any(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday) == week));
			}

			// Verify Days
			for (int day = 0; day <= _addonOptions.GenerationalDays; day++)
			{
				Assert.IsTrue(_onedriveBackups.Single(backup => backup.BackupDate.Date.Equals(_dateTimeProvider.Now.Date.AddDays(-day))) != null);
			}
		}


		#endregion

		private void SetupHassIoClient()
		{
			_hassIoClientMock = new Mock<IHassioClient>();
			_hassIoClientMock
				.Setup(hassIoClient => hassIoClient.CreateBackupAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), true, It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync((string backupName, DateTime timeStamp, bool appendTS, bool compressed, string password, IEnumerable<string> folders, IEnumerable<string> addons) =>
				{
					_localBackups.Add(new Backup()
					{
						Date = _dateTimeProvider.Now,
						Slug = Guid.NewGuid().ToString(),
						Protected = string.IsNullOrEmpty(password) ? false : true,
						Compressed = compressed,
						Name = backupName
					});

					return true;
				});
			_hassIoClientMock.Setup(client => client.GetBackupsAsync(It.IsAny<Predicate<Backup>>()))
				.ReturnsAsync((Predicate<Backup> filter) =>
				{
					var ret = _localBackups.Where(backup => filter(backup)).ToList();
					return ret;
				});

			_hassIoClientMock.Setup(client => client.DownloadBackupAsync(It.IsAny<string>()))
				.ReturnsAsync((string slug) => slug);

			_hassIoClientMock.Setup(client => client.DeleteBackupAsync(It.IsAny<Backup>()))
				.ReturnsAsync((Backup backup) =>
				{
					return _localBackups.Remove(backup);
				});
			
		}

		private void SetupIGraphHelper()
		{
			_graphHelperMock = new Mock<IGraphHelper>();
			_graphHelperMock.Setup(gh => gh.GetItemsInAppFolderAsync(""))
				.ReturnsAsync(() => _onedriveBackups.Select(backup => new DriveItem()
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
