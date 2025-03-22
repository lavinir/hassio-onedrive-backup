using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Services.Mocks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

string serviceName = "HassOneDriveBackup";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register service implementations
builder.Services.AddScoped<IBackupService, MockBackupService>();
builder.Services.AddSingleton<IOneDriveAuthService, OneDriveAuthService>(); // Changed to singleton for persistent token storage
builder.Services.AddScoped<ISettingsService, MockSettingsService>();

// Configure Grafana Cloud telemetry
builder.Services.Configure<GrafanaTelemetryOptions>(builder.Configuration.GetSection("GrafanaTelemetry"));
builder.Services.AddSingleton<ITelemetryManager, GrafanaTelemetryManager>();

// Get telemetry options from configuration
var telemetryOptions = builder.Configuration.GetSection("GrafanaTelemetry").Get<GrafanaTelemetryOptions>() 
    ?? new GrafanaTelemetryOptions();

// Use the service name from configuration
serviceName = !string.IsNullOrEmpty(telemetryOptions.ServiceName) 
    ? telemetryOptions.ServiceName 
    : serviceName;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: telemetryOptions.ServiceVersion))
        .AddSource(serviceName) 
        .AddOtlpExporter(options =>
        {
            // Set endpoint and API key from configuration
            if (!string.IsNullOrEmpty(telemetryOptions.OtlpEndpoint))
            {
                options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
            }
            
            if (!string.IsNullOrEmpty(telemetryOptions.ApiKey))
            {
                options.Headers = $"Authorization=Basic {telemetryOptions.ApiKey}";
            }
        })
    )
    .WithMetrics(metrics => metrics
        .AddMeter(serviceName) 
        .AddOtlpExporter(options =>
        {
            // Set endpoint and API key from configuration (same as tracing)
            if (!string.IsNullOrEmpty(telemetryOptions.OtlpEndpoint))
            {
                options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
            }
            
            if (!string.IsNullOrEmpty(telemetryOptions.ApiKey))
            {
                options.Headers = $"Authorization=Basic {telemetryOptions.ApiKey}";
            }
        })
    );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();