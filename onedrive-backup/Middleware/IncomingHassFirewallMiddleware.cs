using hassio_onedrive_backup;
using Microsoft.Extensions.Primitives;

namespace onedrive_backup.Middleware
{
	public class IncomingHassFirewallMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly IHostEnvironment _env;
		private readonly ConsoleLogger _logger;
		private const string _allowedIP = "172.30.32.2";
		
		public IncomingHassFirewallMiddleware(RequestDelegate next, IHostEnvironment env, ConsoleLogger logger)
		{
			_next = next;
			_env = env;
			_logger = logger;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var ip = context.Connection.RemoteIpAddress.MapToIPv4();
			if (_env.IsDevelopment() ==false && ip.ToString().Equals(_allowedIP, StringComparison.OrdinalIgnoreCase) == false)
			{
				_logger.LogError($"Blocking request from unauthorized source IP : {ip.ToString()}");
			}

			// await context.Response.WriteAsync($"Request origin ({ip}) blocked.");
			await _next(context);
		}

	}
}
