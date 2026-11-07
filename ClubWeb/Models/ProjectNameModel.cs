using System.ComponentModel.DataAnnotations;

namespace ClubWeb.Models;

public sealed class ProjectNameModel
{
    [Required(ErrorMessage = "Enter a project name (not just spaces).")]
    [StringLength(100, ErrorMessage = "Use 100 characters or fewer.")]
    public string Name { get; set; } = "";
}
