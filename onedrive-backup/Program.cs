using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;
using hassio_onedrive_backup.Sync;
using Microsoft.Extensions.FileProviders;
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
        private static string _pathBase = "";
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
            IHassioClient hassIoClient = new HassioClient(supervisorToken, TimeSpan.FromMinutes(addonOptions.HassAPITimeoutMinutes));
#endif
            LocalStorage.InitializeTempStorage();
            IGraphHelper graphHelper = new GraphHelper(scopes, clientId);
            var addonInfo = hassIoClient.GetAddonInfo("local_hassio_onedrive_backup").Result;
            _pathBase = addonInfo.DataProperty.IngressUrl;
            ConsoleLogger.LogInfo($"Ingress Info. Entry: {addonInfo.DataProperty.IngressEntry}. URL: {addonInfo.DataProperty.IngressUrl}");

            var builder = WebApplication.CreateBuilder(args);

            // builder.Logging.AddConsole();

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddSingleton(new IngressSettings { IngressUrl = _pathBase });
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


            app.UsePathBase($"{_pathBase}");

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


//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddControllersWithViews();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}

//app.UseStaticFiles();
//app.UseRouting();


//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller}/{action=Index}/{id?}");

//app.MapFallbackToFile("index.html");

//app.Run();