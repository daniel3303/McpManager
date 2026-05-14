using System.Net;

namespace McpManager.Web.Portal.Extensions;

public static class HttpContextExtensions
{
    public static string GetAction(this HttpContext httpContext) =>
        httpContext.Request.RouteValues["Action"]?.ToString() ?? string.Empty;

    public static string GetController(this HttpContext httpContext) =>
        httpContext.Request.RouteValues["Controller"]?.ToString() ?? string.Empty;

    public static IPAddress GetRemoteIpAddress(this HttpContext httpContext)
    {
        // Try to get IP from Cloudflare's custom header
        var ip = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();

        // Fallback to X-Forwarded-For if CF-Connecting-IP isn't available
        if (string.IsNullOrEmpty(ip))
        {
            ip = httpContext.Connection.RemoteIpAddress?.ToString();
        }

        return IPAddress.TryParse(ip, out var ipAddress) ? ipAddress : null;
    }

    public static bool IsAjaxRequest(this HttpRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "Request cannot be null");
        }

        return request.Headers.ContainsKey("X-Requested-With")
            && request.Headers["X-Requested-With"] == "XMLHttpRequest";
    }
}
