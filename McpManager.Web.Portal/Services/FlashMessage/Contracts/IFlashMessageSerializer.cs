namespace McpManager.Web.Portal.Services.FlashMessage.Contracts;

public interface IFlashMessageSerializer
{
    List<IFlashMessageModel> Deserialize(string data);
    string Serialize(IList<IFlashMessageModel> messages);
}
