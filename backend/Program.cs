using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Services.Mocks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register service implementations
builder.Services.AddScoped<IBackupService, MockBackupService>();
builder.Services.AddSingleton<IOneDriveAuthService, OneDriveAuthService>(); // Changed to singleton for persistent token storage
builder.Services.AddScoped<ISettingsService, MockSettingsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();