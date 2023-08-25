using Kusto.Data;
using Kusto.Ingest;

namespace onedrive_backup.Telemetry
{
	public class TelemetryManager
	{
		private const string appId = "560e1794-229c-4e58-8b22-a1675e4db7dc";
		private const string appKey = "guJ8Q~WDYfjPXgwT6yzs8aZT4kqruVGDxxFCEaQr";
		private const string tenantId = "fe1e0daa-f3d1-4177-995a-f2687da13b25";
		private KustoConnectionStringBuilder _kcsb;
		private Guid _clientId;

		public TelemetryManager()
		{
			_kcsb = new KustoConnectionStringBuilder($"https://onedrive-addon-telem.westeurope.kusto.windows.net")
				.WithAadApplicationKeyAuthentication(
					appId,
					appKey,
					tenantId);

			_clientId = CheckClientId();
		}

		private Guid CheckClientId()
		{
			throw new NotImplementedException();
		}

		public async Task SendConfig()
		{
			using var client = KustoIngestFactory.CreateQueuedIngestClient(
				_kcsb,
				new QueueOptions { MaxRetries = 2 });

			var ingestionProperties = new KustoIngestionProperties("telemetry", "configs")
			{
				Format = Kusto.Data.Common.DataSourceFormat.json,
			};

			var memoryStream = new MemoryStream();
			using (var streamWriter = new StreamWriter(memoryStream))
			{								
					streamWriter.Write(line);				
			}
			memoryStream.Position = 0;
			return memoryStream;
			client.IngestFromStreamAsync()

		}
	}
}
