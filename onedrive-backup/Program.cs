using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;
using hassio_onedrive_backup.Sync;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Console;
using onedrive_backup;
using onedrive_backup.Extensions;
using onedrive_backup.Hass;
using System.Collections;

namespace hassio_onedrive_backup
{
    public class Program
    {
        private const string clientId = "b8a647cf-eccf-4c7f-a0a6-2cbec5d0b94d";
        private const string addonDirectory = "/data";
        private static readonly List<string> scopes = new() { "Files.ReadWrite.AppFolder" };

        private static Orchestrator _orchestrator;
        private static string _baseDirectory;

        static void Main(string[] args)
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

#if DEBUG
            IHassioClient hassIoClient = new HassioClientMock();
            var addonOptions = AddonOptionsReader.ReadOptions();
#else

            Directory.SetCurrentDirectory(addonDirectory);
            var addonOptions = AddonOptionsReader.ReadOptions();
            string supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN")!;
            IHassioClient hassIoClient = new HassioClient(supervisorToken, addonOptions);
#endif
            ConsoleLogger.SetLogLevel(addonOptions.LogLevel);
            ConsoleLogger.LogVerbose("VerboseTest");
            ConsoleLogger.LogInfo("InfoTest");
            ConsoleLogger.LogWarning("WarningTest");
            ConsoleLogger.LogError("ErrorTest");
			var builder = WebApplication.CreateBuilder(args);
			LocalStorage.InitializeTempStorage();
            IGraphHelper graphHelper = new GraphHelper(scopes, clientId);
			var addons = hassIoClient.GetAddonsAsync().Result;
            HassContext hassContext = null;
			var addonInfo = hassIoClient.GetAddonInfo("self").Result;
			hassContext = new HassContext { IngressUrl = addonInfo.DataProperty.IngressUrl, Addons = addons };
			ConsoleLogger.LogInfo($"Ingress Info. Entry: {addonInfo.DataProperty.IngressEntry}. URL: {addonInfo.DataProperty.IngressUrl}");
			builder.Services.AddSingleton(hassContext);

			//ConsoleLogger.LogInfo($"Detected Addons: {string.Join(",", addons.Select(a => a.Name))}");
            
            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddSingleton<ComponentInitializedStateHelper>();
            builder.Services.AddSingleton(addonOptions);
            builder.Services.AddSingleton<IHassioClient>(hassIoClient);
            builder.Services.AddSingleton<IGraphHelper>(graphHelper);
            
            // var hassOneDriveEntityState = HassOnedriveEntityState.Initialize(hassIoClient);
            builder.Services.AddSingleton<HassOnedriveEntityState>();

            // var hassOneDriveFileSyncEntityState = HassOnedriveFileSyncEntityState.Initialize(hassIoClient);
            builder.Services.AddSingleton<HassOnedriveFileSyncEntityState>();
            builder.Services.AddSingleton<HassOnedriveFreeSpaceEntityState>();
            builder.Services.AddSingleton<Orchestrator>();
            // builder.WebHost.UseStaticWebAssets();
            builder.WebHost.UseUrls("http://*:8099");

            if (!builder.Environment.IsDevelopment())
            {
                builder.Logging.ClearProviders();
                // builder.Logging.AddConsole();
			}

            var app = builder.Build();
            _orchestrator = app.Services.GetService<Orchestrator>();
            _orchestrator.Start();

            if (!app.Environment.IsDevelopment())
            {
                app.UseWhen(ctx => !ctx.Request.Path
                .StartsWithSegments("/_framework/blazor.server.js"),
                    subApp => subApp.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider($"{_baseDirectory}/wwwroot")
                    }));
            }
            else
            {
                ConsoleLogger.LogInfo("Dev Mode");
            }


            app.UsePathBase($"{hassContext?.HeaderIngressPath ?? "/"}");

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseHassUrlExtractor();

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");
            app.Run();
        }
	}
}