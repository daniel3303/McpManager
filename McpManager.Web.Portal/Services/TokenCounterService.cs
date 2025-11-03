using Equibles.Core.AutoWiring;
using Microsoft.ML.Tokenizers;

namespace McpManager.Web.Portal.Services;

[Service(ServiceLifetime.Singleton)]
public class TokenCounterService {
    private readonly TiktokenTokenizer _tokenizer;

    public TokenCounterService() {
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
    }

    /// <summary>
    /// Count tokens in a text string.
    /// </summary>
    public int CountTokens(string text) {
        if (string.IsNullOrEmpty(text)) return 0;
        return _tokenizer.CountTokens(text);
    }

    /// <summary>
    /// Count tokens for a tool (description + input schema combined).
    /// </summary>
    public int CountToolTokens(string description, string inputSchema) {
        var text = (description ?? "") + " " + (inputSchema ?? "{}");
        return CountTokens(text);
    }
}
