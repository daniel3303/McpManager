namespace McpManager.Web.Portal.Authorization;

public static class ClaimStore {
    private static readonly List<ApplicationClaimGroup> Claims = [
        new("Administration", [
            new ApplicationClaim("Admin", "Administrator", "Full administrative access"),
        ]),

        new("Users", [
            new ApplicationClaim("Users", "Users", "Can view and manage users"),
        ]),

        new("MCP", [
            new ApplicationClaim("McpServers", "MCP Servers", "Can view and manage MCP servers"),
            new ApplicationClaim("McpNamespaces", "MCP Namespaces", "Can view and manage MCP namespaces"),
        ]),

        new("API Keys", [
            new ApplicationClaim("ApiKeys", "API Keys", "Can view and manage API keys"),
        ]),
    ];

    public static List<ApplicationClaimGroup> ClaimGroups() => Claims;

    public static List<ApplicationClaim> ClaimList() => Claims.SelectMany(g => g.Claims).ToList();

    public static ApplicationClaim Get(string type) => ClaimList().First(c => c.Type == type);
}
