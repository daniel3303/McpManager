namespace McpManager.Web.Portal.Services.FlashMessage.Contracts;

public interface IFlashMessage
{
    List<IFlashMessageModel> Peek();
    List<IFlashMessageModel> Retrieve();
    void Clear();
    void Success(string message, string title = null, bool isHtml = false);
    void Error(string message, string title = null, bool isHtml = false);
    void Info(string message, string title = null, bool isHtml = false);
    void Warning(string message, string title = null, bool isHtml = false);
}
