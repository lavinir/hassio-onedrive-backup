using hassio_onedrive_backup;
using hassio_onedrive_backup.Contracts;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Text;

namespace onedrive_backup.Telemetry
{
	public class TelemetryManager
	{
		private const string AppId = "560e1794-229c-4e58-8b22-a1675e4db7dc";
		private const string AppKey = "guJ8Q~WDYfjPXgwT6yzs8aZT4kqruVGDxxFCEaQr";
		private const string TenantId = "fe1e0daa-f3d1-4177-995a-f2687da13b25";
		private const string ClientIdPath = ".clientId";

		private KustoConnectionStringBuilder _kcsb;
		private Guid _clientId;
		private readonly ConsoleLogger _logger;

		public TelemetryManager(ConsoleLogger logger)
		{
			_kcsb = new KustoConnectionStringBuilder($"https://kvc-j6ah0s938pt6z0nead.northeurope.kusto.windows.net")
				.WithAadApplicationKeyAuthentication(
					AppId,
					AppKey,
					TenantId);

			_clientId = CheckClientId();
			_logger = logger;
		}

		private Guid CheckClientId()
		{
			if (File.Exists(ClientIdPath) == false)
			{
				var clientId = Guid.NewGuid();
				File.WriteAllText(ClientIdPath, clientId.ToString());
				return clientId;
			}

			string clientIdStr = File.ReadAllText(ClientIdPath);
			return Guid.Parse(clientIdStr);
		}

		public async Task SendConfig(AddonOptions options)
		{
			try
			{
				var configTelemetry = new
				{
					FileSyncEnabled = options.FileSyncEnabled,
					GenerationalBackupsEnabled = options.GenerationalBackups,
					AllowedHoursEnabled = string.IsNullOrWhiteSpace(options.BackupAllowedHours) == false,
					InstanceNameEnabled = string.IsNullOrEmpty(options.InstanceName) == false,
					MonitorAllBackups = options.MonitorAllLocalBackups,
					IgnoreHassUpgradeBackups = options.IgnoreUpgradeBackups,
					NotifyOnErrorEnabled = options.NotifyOnError,
					Version = AddonOptions.AddonVersion
				};

				var telemetryMsg = new
				{
					clientid = _clientId,
					timestamp = DateTime.UtcNow,
					configdata = configTelemetry
				};

				var serializedMsg = JsonConvert.SerializeObject(telemetryMsg, Formatting.None);

				using var client = KustoIngestFactory.CreateQueuedIngestClient(
					_kcsb,
					new QueueOptions { MaxRetries = 2 });

				var ingestionProperties = new KustoIngestionProperties("HassOneDriveTelemetry", "configs")
				{
					Format = Kusto.Data.Common.DataSourceFormat.json,
					IngestionMapping = new IngestionMapping
					{
						IngestionMappingKind = Kusto.Data.Ingestion.IngestionMappingKind.Json,
						IngestionMappings = new[]
						{
							new ColumnMapping("clientid", "guid", new Dictionary<string, string>{ {"Path", "$.clientid" } }),
							new ColumnMapping("timestamp", "datetime", new Dictionary<string, string>{ {"Path", "$.timestamp" } }),
							new ColumnMapping("configdata", "dynamic", new Dictionary<string, string>{ {"Path", "$.configdata" } }),
						}
					}
				};

				await SendToKusto(serializedMsg, client, ingestionProperties);

			}
			catch (Exception e)
			{
				_logger.LogError(e.ToString());
			}		
		}

		public async Task SendError(Exception ex)
		{
			try
			{
				var telemetryMsg = new
				{
					clientId = _clientId,
					Timestamp = DateTime.UtcNow,
					error = ex.ToString()
				};

				var serializedMsg = JsonConvert.SerializeObject(telemetryMsg, Formatting.None);

				using var client = KustoIngestFactory.CreateQueuedIngestClient(
					_kcsb,
					new QueueOptions { MaxRetries = 2 });

				var ingestionProperties = new KustoIngestionProperties("telemetry", "error")
				{
					Format = Kusto.Data.Common.DataSourceFormat.json,
				};

				await SendToKusto(serializedMsg, client, ingestionProperties);

			}
			catch (Exception e)
			{
				_logger.LogError(e.ToString());
			}
		}

		private static async Task SendToKusto(string serializedMsg, IKustoQueuedIngestClient client, KustoIngestionProperties ingestionProperties)
		{
			var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMsg));
			var result = await client.IngestFromStreamAsync(memoryStream, ingestionProperties);
		}
	}
}
