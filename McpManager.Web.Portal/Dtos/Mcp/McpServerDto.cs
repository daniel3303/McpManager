using System.ComponentModel.DataAnnotations;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Dtos.Contracts;

namespace McpManager.Web.Portal.Dtos.Mcp;

public class McpServerDto : IHasAuth, IHasCustomHeaders
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    [Display(Name = "Name")]
    public string Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    [Display(Name = "Description")]
    public string Description { get; set; }

    [Required(ErrorMessage = "Transport Type is required")]
    [Display(Name = "Transport Type")]
    public McpTransportType? TransportType { get; set; }

    // HTTP fields
    [MaxLength(2000, ErrorMessage = "URI cannot exceed 2000 characters")]
    [Display(Name = "Server URI")]
    public string Uri { get; set; }

    public AuthDto Auth { get; set; } = new();

    [Display(Name = "Custom Headers")]
    public List<CustomHeaderDto> CustomHeaders { get; set; } = [];

    // Stdio fields — Simple mode (npx package)
    [Display(Name = "NPM Package")]
    [MaxLength(500)]
    public string NpmPackage { get; set; }

    [Display(Name = "Additional Arguments")]
    public string ExtraArguments { get; set; }

    // Stdio fields — Advanced mode toggle
    [Display(Name = "Advanced Command")]
    public bool UseAdvancedCommand { get; set; }

    [MaxLength(500)]
    [Display(Name = "Command")]
    public string Command { get; set; }

    [Display(Name = "Arguments")]
    public string ArgumentsText { get; set; }

    [Display(Name = "Environment Variables")]
    public List<CustomHeaderDto> EnvVars { get; set; } = [];

    // OpenAPI fields
    [Display(Name = "OpenAPI Specification")]
    public string OpenApiSpecification { get; set; }

    [Display(Name = "Spec URL")]
    public string OpenApiSpecUrl { get; set; }

    // OpenAPI tool customizations (applied after sync)
    public List<McpToolCustomizationDto> ToolCustomizations { get; set; } = [];
}
