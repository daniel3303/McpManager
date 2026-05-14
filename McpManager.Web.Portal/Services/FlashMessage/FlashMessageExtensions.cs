using McpManager.Web.Portal.Services.FlashMessage.Contracts;

namespace McpManager.Web.Portal.Services.FlashMessage;

public static class FlashMessageExtensions
{
    public static void AddFlashMessage(this IServiceCollection services)
    {
        services.AddTransient<IFlashMessage, FlashMessage>();
        services.AddTransient<IFlashMessageSerializer, JsonFlashMessageSerializer>();
    }
}
