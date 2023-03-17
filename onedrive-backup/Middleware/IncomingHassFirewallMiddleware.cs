using hassio_onedrive_backup;
using Microsoft.Extensions.Primitives;

namespace onedrive_backup.Middleware
{
	public class IncomingHassFirewallMiddleware
	{
		private readonly RequestDelegate _next;
		private const string _allowedIP = "172.30.32.2";

		public IncomingHassFirewallMiddleware(RequestDelegate next, IHostEnvironment env)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var ip = context.Connection.RemoteIpAddress.MapToIPv4();
			ConsoleLogger.LogVerbose($"Source IP: {ip}");
			// await context.Response.WriteAsync($"Request origin ({ip}) blocked.");
			await _next(context);
		}

	}
}
