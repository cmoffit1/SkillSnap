using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Client.Pages;

public sealed class AccountFormModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;
}
