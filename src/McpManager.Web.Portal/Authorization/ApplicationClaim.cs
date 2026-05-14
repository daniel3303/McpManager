using System.Security.Claims;

namespace McpManager.Web.Portal.Authorization;

public class ApplicationClaim : Claim
{
    public string Name { get; }
    public string Description { get; }

    public ApplicationClaim(string type, string name, string description)
        : base(type, type)
    {
        Name = name;
        Description = description;
    }
}
