namespace McpManager.Web.Portal.Authorization;

public class ApplicationClaimGroup {
    public string Name { get; }
    public ApplicationClaim[] Claims { get; }

    public ApplicationClaimGroup(string name, ApplicationClaim[] claims) {
        Name = name;
        Claims = claims;
    }
}
