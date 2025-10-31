namespace McpManager.Web.Portal.Services.FlashMessage.Contracts;

public interface IFlashMessageModel {
    bool IsHtml { get; set; }

    string Title { get; set; }

    string Message { get; set; }

    FlashMessageType Type { get; set; }
}
