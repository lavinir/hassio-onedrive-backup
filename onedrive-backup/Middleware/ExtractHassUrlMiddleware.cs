using hassio_onedrive_backup;
using Microsoft.Extensions.Primitives;
using onedrive_backup.Hass;

namespace onedrive_backup.Middleware
{
    public class ExtractHassUrlMiddleware
    {
        private readonly RequestDelegate _next;
        private HassContext _ingressSettings;
		private IHostEnvironment _env;
		private ConsoleLogger _logger;

		public ExtractHassUrlMiddleware(RequestDelegate next, HassContext ingressSettings, IHostEnvironment env, ConsoleLogger logger)
        {
            _next = next;
            _ingressSettings = ingressSettings;
            _env = env;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            const string HeaderKeyName = "X-Ingress-Path";
            if (context.Request.Headers.TryGetValue(HeaderKeyName, out StringValues headerValue))
            {
				_ingressSettings.HeaderIngressPath = headerValue.ToString();
                _logger.LogInfo($"X-Ingress-Path: {_ingressSettings.HeaderIngressPath}");
			}
            else if (_env.IsDevelopment())
            {
                _ingressSettings.HeaderIngressPath = "/";
            }
            
			await _next(context);
        }
    }
}
