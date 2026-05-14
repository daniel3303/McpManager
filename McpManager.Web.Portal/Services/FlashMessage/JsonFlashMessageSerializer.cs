using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using Newtonsoft.Json;

namespace McpManager.Web.Portal.Services.FlashMessage;

public class JsonFlashMessageSerializer : IFlashMessageSerializer
{
    public List<IFlashMessageModel> Deserialize(string data)
    {
        var result = JsonConvert.DeserializeObject<List<FlashMessageModel>>(data);
        return result?.Cast<IFlashMessageModel>().ToList() ?? new List<IFlashMessageModel>();
    }

    public string Serialize(IList<IFlashMessageModel> messages)
    {
        return JsonConvert.SerializeObject(messages);
    }
}
