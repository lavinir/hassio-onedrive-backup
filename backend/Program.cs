using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Services.Mocks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.Text.Json;
using System.Text.Json.Serialization;

string serviceName = "HassOneDriveBackup";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
    
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register service implementations
builder.Services.AddSingleton<IBackupService, BackupService>();
builder.Services.AddSingleton<IOneDriveClient, OneDriveClient>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddSingleton<IRetentionPolicyService, RetentionPolicyService>();
builder.Services.AddSingleton<RetentionDataStore>();


bool developmentMode = builder.Configuration.GetValue<bool>("DevelopmentMode");
if (developmentMode)
{
    builder.Services.AddSingleton<IHassioClient, MockHassioClient>();
}
else
{
    builder.Services.AddSingleton<IHassioClient, HassioClient>();
}

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

// Configure OpenTelemetry manually (without ASP.NET Core automatic integration)
// This only sets up the SDK for our explicit manual telemetry from GrafanaTelemetryManager

// Configure the TracerProvider
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: telemetryOptions.ServiceVersion))
    .AddSource(serviceName)  // Only add our own ActivitySource, not ASP.NET's
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
    .Build();

// Configure the MeterProvider
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: telemetryOptions.ServiceVersion))
    .AddMeter(serviceName)  // Only add our own Meter, not ASP.NET's
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
    .Build();

// Register providers to be disposed when application shuts down
builder.Services.AddSingleton(tracerProvider);
builder.Services.AddSingleton(meterProvider);

var app = builder.Build();

// Initialize local storage (creates temp dir, cleans up legacy artifacts)
LocalStorage.InitializeStorage(app.Services.GetRequiredService<ILogger<LocalStorage>>());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
