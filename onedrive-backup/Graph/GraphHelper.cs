using Azure.Core;
using Azure.Identity;
using hassio_onedrive_backup.Storage;
using Microsoft.Graph;
using onedrive_backup.Contracts;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using File = System.IO.File;

namespace hassio_onedrive_backup.Graph
{
	public class GraphHelper : IGraphHelper
	{
		private const string AuthRecordFile = "record.auth";
		private const int UploadRetryCount = 3;
		private const int DownloadRetryCount = 3;
		private const int GraphRequestTimeoutMinutes = 2;
		private const int ChunkSize = (320 * 1024) * 10;
		private DeviceCodeCredential? _deviceCodeCredential;
		protected GraphServiceClient? _userClient;
		private IEnumerable<string> _scopes;
		private string _clientId;
		private string _persistentDataPath;
		private HttpClient _downloadHttpClient;
		private bool? _isAuthenticated = null;

		public event AuthStatusChanged? AuthStatusChangedEventHandler;

		public GraphHelper(
			IEnumerable<string> scopes,
			string clientId,
			string persistentDataPath = "")
		{
			_scopes = scopes;
			_clientId = clientId;
			_persistentDataPath = persistentDataPath;
		}

		public bool? IsAuthenticated
		{
			get => _isAuthenticated; 
			private set
			{
				_isAuthenticated = value; AuthStatusChangedEventHandler?.Invoke();
			}
		}

        public string AuthUrl { get; private set; }

        public string AuthCode { get; private set; }

		private string PersistentAuthRecordFullPath => Path.Combine(_persistentDataPath, AuthRecordFile);

		public async Task<string> GetAndCacheUserTokenAsync()
		{
			if (_deviceCodeCredential == null)
			{
				await InitializeGraphForUserAuthAsync();
			}

			_ = _deviceCodeCredential ??
				throw new NullReferenceException("User Auth not Initialized");

			_ = _scopes ?? throw new ArgumentNullException("'scopes' cannot be null");

			var context = new TokenRequestContext(_scopes.ToArray());
			var response = await _deviceCodeCredential.GetTokenAsync(context);
			await PersistAuthenticationRecordAsync(GetAuthenticationRecordFromCredential());
			IsAuthenticated = true;
			return response.Token;
		}

		public async Task<DriveItem?> GetItemInAppFolderAsync(string subPath = "")
		{
			try
			{
				var item = await _userClient.Me.Drive.Special.AppRoot.ItemWithPath(subPath).Request().Expand("children").GetAsync();
				return item;
				// return item.Children.ToList();
			}
			catch (ServiceException se)
			{
				if (se.StatusCode != System.Net.HttpStatusCode.NotFound)
				{
					throw;
				}

				return null;
			}

		}

		public async Task<List<DriveItem>?> GetItemsInAppFolderAsync(string subPath = "")
		{
			var parent = await GetItemInAppFolderAsync(subPath);
			return parent?.Children?.ToList();
		}

		public async Task<bool> DeleteItemFromAppFolderAsync(string itemPath)
		{
			try
			{
				ConsoleLogger.LogInfo($"Deleting item: {itemPath}");
				await _userClient.Drive.Special.AppRoot.ItemWithPath(itemPath).Request().DeleteAsync();
			}
			catch (Exception ex)
			{
				ConsoleLogger.LogError($"Error deleting {itemPath}. {ex}");
				return false;
			}

			return true;
		}

		public async Task<DriveItem> GetOrCreateFolder(string folderPath)
		{
			var folder = (await GetItemInAppFolderAsync(folderPath)) ??
				await _userClient.Drive.Special.AppRoot.ItemWithPath(folderPath).Children.Request().AddAsync(new DriveItem
				{
					// Name = Path.GetFileName(folderPath),
					// Folder = new Folder { }
					File = new Microsoft.Graph.File { },
					Name = "temp.txt",
					Content = new MemoryStream(Encoding.UTF8.GetBytes("Here's your damn content"))
				});

			return folder;
		}

		public async Task<bool> UploadFileAsync(string filePath, DateTime date, string? instanceName, string? destinationFileName = null, Action<int>? progressCallback = null, bool flatten = true, string description = null)
		{
			if (File.Exists(filePath) == false)
			{
				ConsoleLogger.LogError($"File {filePath} not found");
				return false;
			}

			using var fileStream = File.OpenRead(filePath);
			destinationFileName = destinationFileName ?? (flatten ? Path.GetFileName(filePath) : filePath);
			// string originalFileName = Path.GetFileNameWithoutExtension(filePath);
			var uploadSession = await _userClient.Drive.Special.AppRoot.ItemWithPath(destinationFileName).CreateUploadSession(new DriveItemUploadableProperties
			{
				Description = description // ? null : SerializeBackupDescription(originalFileName, date, instanceName)
			}

			).Request().PostAsync();

			// todo: allow settings this in advanced configuration
			int maxSlizeSize = ChunkSize;
			long totalFileLength = fileStream.Length;
			var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSlizeSize);
			var lastShownPercentageHolder = new UploadProgressHolder();
			IProgress<long> progress = new Progress<long>(prog =>
			{
				double percentage = Math.Round((prog / (double)totalFileLength), 2) * 100;
				if (percentage - lastShownPercentageHolder.Percentage >= 10 || percentage == 100)
				{
					ConsoleLogger.LogInfo($"Uploaded {percentage}%");
					lastShownPercentageHolder.Percentage = percentage;
				}

				progressCallback?.Invoke((int)percentage);
			});

			int uploadAttempt = 0;
			while (uploadAttempt++ < UploadRetryCount)
			{
				try
				{
					ConsoleLogger.LogInfo($"Starting file upload. (Size:{totalFileLength} bytes. Attempt: {uploadAttempt}/{UploadRetryCount})");
					UploadResult<DriveItem> uploadResult;
					if (uploadAttempt > 1)
					{
						uploadResult = await fileUploadTask.ResumeAsync(progress);
					}
					else
					{
						uploadResult = await fileUploadTask.UploadAsync(progress);
					}

					await Task.Delay(TimeSpan.FromSeconds(2));
					if (uploadResult.UploadSucceeded)
					{
						ConsoleLogger.LogInfo("Upload completed successfully");
						break;
					}
					else
					{
						ConsoleLogger.LogError("Upload failed");
					}
				}
				catch (ServiceException ex)
				{
					ConsoleLogger.LogError($"Error uploading: {ex}");
					return false;
				}
			}

			return true;
		}

		public async Task<OneDriveFreeSpaceData> GetFreeSpaceInGB()
		{
			try
			{
				var drive = await _userClient.Drive.Request().GetAsync();
				double? totalSpace = drive.Quota.Total == null ? null : drive.Quota.Total.Value / (double)Math.Pow(1024, 3);
				double? freeSpace = drive.Quota.Remaining == null ? null : drive.Quota.Remaining.Value / (double)Math.Pow(1024, 3);
				return new OneDriveFreeSpaceData
				{
					FreeSpace = freeSpace,
					TotalSpace = totalSpace
				};

			}
			catch (Exception ex)
			{
				ConsoleLogger.LogError($"Error getting free space: {ex}");
				return null;
			}
		}

		public async Task<string?> DownloadFileAsync(string fileName, Action<int?>? progressCallback)
		{
			var item = await _userClient.Drive.Special.AppRoot.ItemWithPath(fileName).Request().GetAsync();
			if (item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var downloadUrl) == false)
			{
				ConsoleLogger.LogError($"Failed getting file download data. ${fileName}");
				return null;
			}

			var fileInfo = new FileInfo($"{LocalStorage.TempFolder}/{fileName}");
			using var fileStream = File.Create(fileInfo.FullName);

			_downloadHttpClient = _downloadHttpClient ?? new HttpClient();
			long position = 0;
			int attempt = 1;
			while (position < item.Size)
			{
				try
				{
					long chunkSize = Math.Min(position + ChunkSize, item.Size.Value - 1);
					_downloadHttpClient.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(position, chunkSize);
					var contentStream = await _downloadHttpClient.GetStreamAsync(downloadUrl.ToString());
					await contentStream.CopyToAsync(fileStream);
					position = chunkSize + 1;
					progressCallback?.Invoke((int)(position * 100 / item.Size.Value));
				}
				catch (Exception ex)
				{
					if (attempt >= DownloadRetryCount)
					{
						ConsoleLogger.LogError($"Failed downloading file {fileName}. {ex}");
						progressCallback?.Invoke(null);
						return null;
					}

					await Task.Delay(5000);
				}
			}

			progressCallback?.Invoke(null);
			ConsoleLogger.LogInfo($"{fileName} downloaded successfully");
			return fileInfo.FullName;
		}

		protected virtual async Task InitializeGraphForUserAuthAsync()
		{
			AuthenticationRecord? authRecord = await ReadPersistedAuthenticationRecordAsync();
			var deviceCodeCredOptions = new DeviceCodeCredentialOptions
			{
				ClientId = _clientId,
				DeviceCodeCallback = DeviceCodeBallBackPrompt,
				TenantId = "common",
				AuthenticationRecord = authRecord,
				TokenCachePersistenceOptions = new TokenCachePersistenceOptions
				{
					Name = "hassio-onedrive-backup",
					UnsafeAllowUnencryptedStorage = true
				},
			};

			_deviceCodeCredential = new DeviceCodeCredential(deviceCodeCredOptions);
			_userClient = new GraphServiceClient(_deviceCodeCredential, _scopes);
			_userClient.HttpProvider.OverallTimeout = TimeSpan.FromMinutes(GraphRequestTimeoutMinutes);
		}

		private AuthenticationRecord GetAuthenticationRecordFromCredential()
		{
			var record = typeof(DeviceCodeCredential)
				.GetProperty("Record", System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(_deviceCodeCredential) as AuthenticationRecord;

			return record;
		}

		private async Task PersistAuthenticationRecordAsync(AuthenticationRecord record)
		{
			using var authRecordStream = new FileStream(PersistentAuthRecordFullPath, FileMode.Create, FileAccess.Write);
			await record.SerializeAsync(authRecordStream);
		}

		private async Task<AuthenticationRecord?> ReadPersistedAuthenticationRecordAsync()
		{
			if (File.Exists(PersistentAuthRecordFullPath) == false)
			{
				ConsoleLogger.LogWarning("Token cache is empty");
				return null;
			}

			using var authRecordStream = new FileStream(PersistentAuthRecordFullPath, FileMode.Open, FileAccess.Read);
			var record = await AuthenticationRecord.DeserializeAsync(authRecordStream);
			return record;
		}

		private Task DeviceCodeBallBackPrompt(DeviceCodeInfo info, CancellationToken ct)
		{
			IsAuthenticated = false;
			ConsoleLogger.LogInfo(info.Message);
			(AuthUrl, AuthCode) = ExtractAuthParams(info.Message);
			return Task.FromResult(0);
		}

        private (string url, string code) ExtractAuthParams(string message)
        {
			Match match = Regex.Match(message, "To sign in, use a web browser to open the page ([^ ]*) and enter the code ([\\w]*) to authenticate");
			string url = match.Groups[1].Value;
			string code = match.Groups[2].Value;
			return (url, code);
        }

        private class UploadProgressHolder
		{
			public double Percentage { get; set; } = 0;
		}
	}
}
