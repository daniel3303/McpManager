using System.ComponentModel.DataAnnotations;

namespace McpManager.Web.Portal.Dtos.ApiKeys;

public class ApiKeyDto {
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    [Display(Name = "Name")]
    public string Name { get; set; }
}
