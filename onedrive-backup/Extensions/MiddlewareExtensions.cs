using onedrive_backup.Middleware;

namespace onedrive_backup.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseHassUrlExtractor(
        this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExtractHassUrlMiddleware>();
        }
    }
}
