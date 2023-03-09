using Microsoft.Extensions.Primitives;
using onedrive_backup.Hass;

namespace onedrive_backup.Middleware
{
    public class ExtractHassUrlMiddleware
    {
        private readonly RequestDelegate _next;
        private IngressSettings _ingressSettings;

        public ExtractHassUrlMiddleware(RequestDelegate next, IngressSettings ingressSettings)
        {
            _next = next;
            _ingressSettings = ingressSettings;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            const string HeaderKeyName = "X-Ingress-Path";
            context.Request.Headers.TryGetValue(HeaderKeyName, out StringValues headerValue);
            _ingressSettings.HeaderIngressPath = headerValue.ToString();
            await _next(context);
        }
    }
}
