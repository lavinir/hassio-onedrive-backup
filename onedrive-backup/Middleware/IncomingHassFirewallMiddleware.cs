using hassio_onedrive_backup;
using Microsoft.Extensions.Primitives;

namespace onedrive_backup.Middleware
{
	public class IncomingHassFirewallMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly IHostEnvironment _env;
		private const string _allowedIP = "172.30.32.2";

		public IncomingHassFirewallMiddleware(RequestDelegate next, IHostEnvironment env)
		{
			_next = next;
			_env = env;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var ip = context.Connection.RemoteIpAddress.MapToIPv4();
			ConsoleLogger.LogVerbose($"Source IP: {ip}");
			if (_env.IsDevelopment() ==false && ip.ToString().Equals(_allowedIP, StringComparison.OrdinalIgnoreCase) == false)
			{
				ConsoleLogger.LogError($"Blocking request from unauthorized source IP : {ip.ToString()}");
			}

			// await context.Response.WriteAsync($"Request origin ({ip}) blocked.");
			await _next(context);
		}

	}
}
