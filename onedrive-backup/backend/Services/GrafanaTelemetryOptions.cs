namespace HassioOneDriveBackup.Services
{
    /// <summary>
    /// Configuration options for Grafana Cloud telemetry
    /// </summary>
    public class GrafanaTelemetryOptions
    {
        /// <summary>
        /// The Grafana Cloud OTLP endpoint URL
        /// Default value is the standard Grafana Cloud OTLP gateway endpoint
        /// </summary>
        public string OtlpEndpoint { get; set; } = "https://otlp-gateway-prod-us-central-0.grafana.net/otlp";
        
        /// <summary>
        /// The Grafana Cloud instance ID
        /// If not provided, a unique instance ID will be automatically generated based on 
        /// environment name, machine name, and network interfaces
        /// </summary>
        public string InstanceId { get; set; } = "";
        
        /// <summary>
        /// The Grafana Cloud API key for authentication with the OTLP endpoint
        /// This should be a valid Grafana Cloud API key with appropriate permissions
        /// </summary>
        public string ApiKey { get; set; } = "";
        
        /// <summary>
        /// The service name to use for telemetry identification in Grafana Cloud
        /// This name will be used to identify this application in metrics and traces
        /// </summary>
        public string ServiceName { get; set; } = "HassioOneDriveBackup";
        
        /// <summary>
        /// The service version to include with telemetry data
        /// This helps track metrics and traces across different versions of your application
        /// </summary>
        public string ServiceVersion { get; set; } = "1.0.0";
    }
}