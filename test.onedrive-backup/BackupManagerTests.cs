using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Microsoft.Graph;
using Moq;
using Newtonsoft.Json;
using onedrive_backup;
using onedrive_backup.Graph;
using onedrive_backup.Hass;
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
			_serviceProviderMock.Setup(provider => provider.GetService(typeof(ConsoleLogger))).Returns(new ConsoleLogger());

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
		public async Task Test_Generational_Retention_3_Days_2_Weeks_4_Months_Max7_Local()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;
			_addonOptions.MaxLocalBackups = 7;

			var testStartDate = _dateTimeProvider.Now;
			int testDays = 120;

			for (int day = 0; day < testDays; day++)
			{
				await _backupManager.PerformBackupsAsync();
				_dateTimeProvider.NextDay();
			}

			Assert.IsTrue(_localBackups.Count == _addonOptions.MaxLocalBackups);
			int monthlyBackups = _localBackups.GroupBy(backup => backup.BackupDate.Date.Month).Count();
			int weeklyBackups = _localBackups.GroupBy(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate.Date, CalendarWeekRule.FirstDay, System.DayOfWeek.Sunday)).Count();

			for (int day = 0; day <= _addonOptions.GenerationalDays; day++)
			{
				Assert.IsTrue(_localBackups.Single(backup => backup.BackupDate.Date.Equals(_dateTimeProvider.Now.Date.AddDays(-day))) != null);
			}
		}



		[TestMethod]
		public async Task Test_Generational_Retention_Mixed1_Local()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;			
			_addonOptions.MaxLocalBackups = 5;

			var now = DateTime.Now;
			_localBackups.AddRange(new List<Backup>
			{
				new Backup
				{
					Slug = "1_keep",           // Day, Week, Month
					Date = now,
					Name = _addonOptions.BackupName
				},
				new Backup
				{
					Slug = "2_keep",           // Week
					Date = now.AddDays(-7),
					Name = _addonOptions.BackupName
				},
				new Backup
				{
					Slug = "3_remove",
					Date = now.AddDays(-8),    
					Name = _addonOptions.BackupName
				},
				new Backup
				{
					Slug = "4_keep",
					Date = now.AddMonths(-2),  //Month 
					Name = _addonOptions.BackupName
				},
				new Backup
				{
					Slug = "5_keep",
					Date = now.AddMonths(-3),  //Month
					Name = _addonOptions.BackupName
				},
				new Backup
				{
					Slug = "6_keep",
					Date = now.AddMonths(-4), //Month
					Name = _addonOptions.BackupName
				}
			});

			await _backupManager.PerformBackupsAsync();
			Assert.IsTrue(_localBackups.Any(backup => backup.Slug.Equals("3_remove") == false));
			Assert.IsTrue(_localBackups.Count() == 5);
		}

		#endregion

		#region OnlineBackupTests

		[TestMethod]
		public async Task Test_Generational_Retention_Days_Online()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxOnedriveBackups = 4;
			_addonOptions.MaxLocalBackups = 0;

			var now = DateTime.Now;
			_onedriveBackups.AddRange(new List<OnedriveBackup>
			{
				new OnedriveBackup
				{
					Slug = "1_keep",
					BackupDate = now,
					FileName = "1_keep"
				},
				new OnedriveBackup
				{
					Slug = "2_keep",
					BackupDate = now.AddDays(-1),
					FileName = "2_keep"
				},
				new OnedriveBackup
				{
					Slug = "3_keep",
					BackupDate = now.AddDays(-2),
					FileName = "3_keep"

				},
				new OnedriveBackup
				{
					Slug = "4_keep",
					BackupDate = now.AddDays(-3),
					FileName = "4_keep"
				},
				new OnedriveBackup
				{
					Slug = "5_remove",
					BackupDate = now.AddDays(-20),
					FileName = "5_remove"
				}

			});

			await _backupManager.PerformBackupsAsync();
			Assert.IsTrue(_onedriveBackups.Any(backup => backup.Slug.Equals("5_remove") == false));
			Assert.IsTrue(_onedriveBackups.Count() == 4);
		}

		[TestMethod]
		public async Task Test_Generational_Retention_Days_Online_NoDelete()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxOnedriveBackups = 5;

			var now = DateTime.Now;
			_onedriveBackups.AddRange(new List<OnedriveBackup>
			{
				new OnedriveBackup
				{
					Slug = "1_keep",
					BackupDate = now,
					FileName = "1_keep"
				},
				new OnedriveBackup
				{
					Slug = "2_keep",
					BackupDate = now.AddDays(-1),
					FileName = "2_keep"
				},
				new OnedriveBackup
				{
					Slug = "3_keep",
					BackupDate = now.AddDays(-2),
					FileName = "3_keep"
				},
				new OnedriveBackup
				{
					Slug = "4_keep",
					BackupDate = now.AddDays(-3),
					FileName = "4_keep"
				},
				new OnedriveBackup
				{
					Slug = "5_keep",
					BackupDate = now.AddDays(-20),
					FileName = "5_keep"
				}

			});

			await _backupManager.PerformBackupsAsync();
			Assert.IsTrue(_onedriveBackups.Count() == 5);
		}

		[TestMethod]
		public async Task Test_Generational_Retention_Days_Online_MaxOnlineOverride()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.MaxOnedriveBackups = 2;

			var now = DateTime.Now;
			_onedriveBackups.AddRange(new List<OnedriveBackup>
			{
				new OnedriveBackup
				{
					Slug = "1_keep",
					BackupDate = now,
					FileName = "1_keep"
				},
				new OnedriveBackup
				{
					Slug = "2_keep",
					BackupDate = now.AddDays(-1),
					FileName = "2_keep"
				},
				new OnedriveBackup
				{
					Slug = "3_remove",
					BackupDate = now.AddDays(-2),
					FileName = "3_remove"

				}
			});

			await _backupManager.PerformBackupsAsync();
			Assert.IsTrue(_onedriveBackups.Any(backup => backup.Slug.Equals("3_remove") == false));
			Assert.IsTrue(_onedriveBackups.Count() == 2);
		}

		[TestMethod]
		public async Task Test_Generational_Retention_Mixed1_Online()
		{
			_addonOptions.GenerationalDays = 3;
			_addonOptions.GenerationalWeeks = 2;
			_addonOptions.GenerationalMonths = 4;
			_addonOptions.MaxOnedriveBackups = 5;

			var now = DateTime.Now;
			_onedriveBackups.AddRange(new List<OnedriveBackup>
			{
				new OnedriveBackup
				{
					Slug = "1_keep",
					BackupDate = now,
					FileName = "1_keep"
				},
				new OnedriveBackup
				{
					Slug = "2_keep",
					BackupDate = now.AddDays(-7),
					FileName = "2_keep",
				},
				new OnedriveBackup
				{
					Slug = "3_remove",
					BackupDate = now.AddDays(-8),
					FileName = "3_remove"
				},
				new OnedriveBackup
				{
					Slug = "4_keep",
					BackupDate = now.AddMonths(-2),
					FileName = "4_keep"
				},
				new OnedriveBackup
				{
					Slug = "5_keep",
					BackupDate = now.AddMonths(-3),
					FileName = "5_keep"
				},
				new OnedriveBackup
				{
					Slug = "6_keep",
					BackupDate = now.AddMonths(-4),
					FileName = "6_keep"
				}
			});

			await _backupManager.PerformBackupsAsync();
			Assert.IsTrue(_onedriveBackups.Any(backup => backup.Slug.Equals("3_remove") == false));
			Assert.IsTrue(_onedriveBackups.Count() == 5);
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
