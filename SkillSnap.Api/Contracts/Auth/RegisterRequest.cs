using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Api.Contracts.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
