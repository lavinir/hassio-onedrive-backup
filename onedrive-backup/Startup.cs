using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;
using Microsoft.AspNetCore.StaticFiles;
using static System.Formats.Asn1.AsnWriter;

namespace hassio_onedrive_backup
{
    public class Startup
    {
        private const string clientId = "b8a647cf-eccf-4c7f-a0a6-2cbec5d0b94d";
        private const string addonDirectory = "/data";
        private static readonly List<string> scopes = new() { "Files.ReadWrite.AppFolder" };

        private Orchestrator _orchestrator;
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

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
            IGraphHelper graphHelper = new GraphHelper(scopes, clientId, (info, cancel) =>
            {
                ConsoleLogger.LogInfo(info.Message);
                return Task.FromResult(0);
            });

            _orchestrator = new Orchestrator(hassIoClient, graphHelper, addonOptions);
            _orchestrator.Start();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            

            // app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //ieb!.MapFallbackToFile("index.html");
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
