using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using System.Text;
using static System.Formats.Asn1.AsnWriter;

namespace hassio_onedrive_backup
{
    public class Startup
    {
        private const string clientId = "b8a647cf-eccf-4c7f-a0a6-2cbec5d0b94d";
        private const string addonDirectory = "/data";
        private static readonly List<string> scopes = new() { "Files.ReadWrite.AppFolder" };

        private Orchestrator _orchestrator;
        private string _pathBase = "";
        private string _indexContent;

        public async void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            _indexContent = File.ReadAllText("ClientApp/public/index.html");

#if DEBUG
            IHassioClient hassIoClient = new HassioClientMock();
            var addonOptions = AddonOptionsReader.ReadOptions();
            // Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
#else            
            ConsoleLogger.LogInfo(AppDomain.CurrentDomain.BaseDirectory);
            Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory, "*", SearchOption.TopDirectoryOnly).ToList().ForEach(ConsoleLogger.LogInfo);
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

            var addonInfo = await hassIoClient.GetAddonInfo("local_hassio_onedrive_backup");
            _pathBase = addonInfo.DataProperty.IngressUrl;
            ConsoleLogger.LogInfo($"Ingress Info. Entry: {addonInfo.DataProperty.IngressEntry}. URL: {addonInfo.DataProperty.IngressUrl}");
            _indexContent = _indexContent.Replace("**baseurl**", addonInfo.DataProperty.IngressUrl);
            // File.WriteAllText("index.html", indexContent);
            _orchestrator = new Orchestrator(hassIoClient, graphHelper, addonOptions);
            _orchestrator.Start();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UsePathBase(_pathBase);

            var staticFileOptions = new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    // Only for Index.HTML file
                    if (context.File.Name == "index.html")
                    {
                        var response = context.Context.Response;
                        var str = _indexContent;
                        // modified stream
                        var responseData = Encoding.UTF8.GetBytes(str);
                        var stream = new MemoryStream(responseData);
                        // set the response body
                        response.Body = stream;
                    }
                }
            };
            app.UseStaticFiles();
            
            app.UseRouting();

            // app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                // endpoints.MapFallback()
                endpoints.MapFallbackToFile("index.html");
            });
       }
    }
}