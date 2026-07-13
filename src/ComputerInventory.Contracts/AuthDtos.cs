using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Contracts;

public class LoginRequest
{
    [Required, StringLength(50)]
    public string Username { get; set; } = string.Empty;
    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}
