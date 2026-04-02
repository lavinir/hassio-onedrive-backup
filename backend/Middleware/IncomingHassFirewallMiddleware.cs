namespace HassioOneDriveBackup.Middleware;

public class IncomingHassFirewallMiddleware
{
    private const string AllowedIp = "172.30.32.2";
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly ILogger<IncomingHassFirewallMiddleware> _logger;

    public IncomingHassFirewallMiddleware(RequestDelegate next, IHostEnvironment env, ILogger<IncomingHassFirewallMiddleware> logger)
    {
        _next = next;
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_env.IsDevelopment())
        {
            var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
            if (!string.Equals(ip, AllowedIp, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Blocking request from unauthorized source IP: {Ip}", ip);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }
}
