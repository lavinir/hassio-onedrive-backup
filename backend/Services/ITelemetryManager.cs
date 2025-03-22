using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace HassioOneDriveBackup.Services
{
    /// <summary>
    /// Interface for telemetry operations using OpenTelemetry
    /// </summary>
    public interface ITelemetryManager
    {
        /// <summary>
        /// Records a simple event occurrence
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        void TrackEvent(string eventName);

        /// <summary>
        /// Records an event with additional properties
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="properties">Additional properties to record with the event</param>
        void TrackEvent(string eventName, IDictionary<string, string> properties);

        /// <summary>
        /// Records an exception that occurred
        /// </summary>
        /// <param name="exception">The exception to record</param>
        /// <param name="properties">Additional properties to record with the exception</param>
        void TrackException(Exception exception, IDictionary<string, string> properties = null);

        /// <summary>
        /// Records a metric value
        /// </summary>
        /// <param name="name">Name of the metric</param>
        /// <param name="value">Value of the metric</param>
        /// <param name="tags">Tags associated with the metric</param>
        void TrackMetric(string name, double value, IDictionary<string, object> tags = null);

        /// <summary>
        /// Creates and returns a counter metric instrument
        /// </summary>
        /// <param name="name">Name of the counter</param>
        /// <param name="unit">Unit of measurement</param>
        /// <param name="description">Description of what the counter measures</param>
        /// <returns>A counter metric instrument</returns>
        Counter<long> CreateCounter(string name, string unit = "", string description = "");
    }
}