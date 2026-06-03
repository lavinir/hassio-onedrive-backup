using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace HassioOneDriveBackup.Services
{
    /// <summary>
    /// Implementation of ITelemetryManager that sends telemetry data to Grafana Cloud
    /// </summary>
    public class GrafanaTelemetryManager : ITelemetryManager
    {
        private readonly ILogger<GrafanaTelemetryManager> _logger;
        private readonly Meter _meter;
        private readonly ActivitySource _activitySource;
        private readonly GrafanaTelemetryOptions _options;
        private readonly string _instanceId;

        /// <summary>
        /// Initializes a new instance of the GrafanaTelemetryManager
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The telemetry configuration options</param>
        /// <param name="environment">The hosting environment</param>
        public GrafanaTelemetryManager(
            ILogger<GrafanaTelemetryManager> logger, 
            IOptions<GrafanaTelemetryOptions> options,
            IHostEnvironment environment)
        {
            _logger = logger;
            _options = options.Value;
            
            // Generate or get the instance ID
            _instanceId = GenerateInstanceId(environment.EnvironmentName);
            
            // Create a meter for metrics with service name and version from options
            _meter = new Meter(_options.ServiceName, _options.ServiceVersion);
            
            // Create an activity source for tracing with service name and version from options
            _activitySource = new ActivitySource(_options.ServiceName, _options.ServiceVersion);
            
            _logger.LogInformation("Initialized Grafana Cloud telemetry manager with endpoint: {Endpoint}, InstanceId: {InstanceId}", 
                _options.OtlpEndpoint, _instanceId);
        }

        /// <summary>
        /// Generates a unique instance ID based on environment and machine characteristics
        /// </summary>
        /// <param name="environmentName">The environment name (e.g., Development, Production)</param>
        /// <returns>A unique instance ID</returns>
        private string GenerateInstanceId(string environmentName)
        {
            // Use the configured instance ID if provided
            if (!string.IsNullOrEmpty(_options.InstanceId))
            {
                return _options.InstanceId;
            }
            
            // Otherwise, generate a unique ID based on available system information
            var machineName = Environment.MachineName;
            var macAddresses = GetMacAddresses();
            
            // Create a unique fingerprint
            var uniqueIdentifier = $"{_options.ServiceName}-{environmentName}-{machineName}-{macAddresses}";
            
            // Hash it for privacy and consistent length
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(uniqueIdentifier));
            var instanceId = Convert.ToBase64String(hash).Substring(0, 16).Replace('/', '_').Replace('+', '-');
            
            return instanceId;
        }

        /// <summary>
        /// Gets a concatenated string of MAC addresses from the local machine
        /// </summary>
        /// <returns>A string representation of MAC addresses</returns>
        private string GetMacAddresses()
        {
            try
            {
                var macAddresses = new StringBuilder();
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                
                foreach (var nic in networkInterfaces)
                {
                    // Only include physical adapters that are up
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        macAddresses.Append(nic.GetPhysicalAddress().ToString());
                    }
                }
                
                return macAddresses.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get MAC addresses for instance ID generation");
                return Guid.NewGuid().ToString(); // Fallback
            }
        }

        // ...existing code...

        /// <inheritdoc/>
        public void TrackEvent(string eventName)
        {
            try
            {
                _logger.LogInformation("Event: {EventName}", eventName);

                using var activity = _activitySource.StartActivity(eventName, ActivityKind.Server);
                if (activity != null)
                {
                    activity.SetTag("event", eventName);
                    activity.SetTag("instance_id", _instanceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking event {EventName}", eventName);
            }
        }

        /// <inheritdoc/>
        public void TrackEvent(string eventName, IDictionary<string, string> properties)
        {
            try
            {
                _logger.LogInformation("Event: {EventName}, Properties: {@Properties}", eventName, properties);

                using var activity = _activitySource.StartActivity(eventName, ActivityKind.Server);
                if (activity != null)
                {
                    activity.SetTag("event", eventName);
                    activity.SetTag("instance_id", _instanceId);
                    
                    // Add all properties as tags
                    if (properties != null)
                    {
                        foreach (var property in properties)
                        {
                            activity.SetTag(property.Key, property.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking event {EventName} with properties", eventName);
            }
        }

        /// <inheritdoc/>
        public void TrackException(Exception exception, IDictionary<string, string> properties = null)
        {
            try
            {
                _logger.LogError(exception, "Exception: {ExceptionMessage}, Properties: {@Properties}", 
                    exception.Message, properties);

                using var activity = _activitySource.StartActivity("Exception", ActivityKind.Server);
                if (activity != null)
                {
                    activity.SetTag("exception.type", exception.GetType().FullName);
                    activity.SetTag("exception.message", exception.Message);
                    activity.SetTag("exception.stacktrace", exception.StackTrace);
                    activity.SetTag("instance_id", _instanceId);
                    
                    // Add all properties as tags
                    if (properties != null)
                    {
                        foreach (var property in properties)
                        {
                            activity.SetTag(property.Key, property.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking exception");
            }
        }

        /// <inheritdoc/>
        public void TrackMetric(string name, double value, IDictionary<string, object> tags = null)
        {
            try
            {
                _logger.LogInformation("Metric: {MetricName}, Value: {MetricValue}, Tags: {@Tags}", 
                    name, value, tags);

                // Create tags for the measurement
                var tagList = new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("instance_id", _instanceId)
                };
                
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        tagList.Add(new KeyValuePair<string, object>(tag.Key, tag.Value));
                    }
                }

                // Record the metric value
                var counter = _meter.CreateCounter<double>(name);
                counter.Add(value, tagList.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking metric {MetricName}", name);
            }
        }

        /// <inheritdoc/>
        public Counter<long> CreateCounter(string name, string unit = "", string description = "")
        {
            try
            {
                // NOTE: We're not directly adding instance_id here because Counter<T> is returned
                // and used by the caller, who will provide tags when recording values
                return _meter.CreateCounter<long>(name, unit, description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating counter {CounterName}", name);
                throw;
            }
        }
    }
}