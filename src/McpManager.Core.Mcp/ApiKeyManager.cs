using System.Security.Cryptography;
using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Microsoft.Extensions.Logging;

namespace McpManager.Core.Mcp;

[Service]
public class ApiKeyManager
{
    private readonly ApiKeyRepository _apiKeyRepository;
    private readonly ILogger<ApiKeyManager> _logger;

    public ApiKeyManager(ApiKeyRepository apiKeyRepository, ILogger<ApiKeyManager> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _logger = logger;
    }

    public async Task<ApiKey> Create(ApiKey apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        apiKey.Key = GenerateApiKey();
        _apiKeyRepository.Add(apiKey);
        await _apiKeyRepository.SaveChanges();
        _logger.LogInformation(
            "Created API key {ApiKeyName} with ID {ApiKeyId}",
            apiKey.Name,
            apiKey.Id
        );
        return apiKey;
    }

    public async Task Delete(ApiKey apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        _apiKeyRepository.Remove(apiKey);
        await _apiKeyRepository.SaveChanges();
        _logger.LogInformation(
            "Deleted API key {ApiKeyName} with ID {ApiKeyId}",
            apiKey.Name,
            apiKey.Id
        );
    }

    public async Task Rename(ApiKey apiKey, string name)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        apiKey.Name = name;
        await _apiKeyRepository.SaveChanges();
        _logger.LogInformation("Renamed API key {ApiKeyId} to {ApiKeyName}", apiKey.Id, name);
    }

    public async Task ToggleActive(ApiKey apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        apiKey.IsActive = !apiKey.IsActive;
        await _apiKeyRepository.SaveChanges();
        _logger.LogInformation(
            "Toggled API key {ApiKeyName} active={IsActive}",
            apiKey.Name,
            apiKey.IsActive
        );
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "mcpm_"
            + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }
}
