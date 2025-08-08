using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Identity;

public enum Theme {
    [Display(Name = "Light")]
    Light = 0,

    [Display(Name = "Dark")]
    Dark = 1
}
