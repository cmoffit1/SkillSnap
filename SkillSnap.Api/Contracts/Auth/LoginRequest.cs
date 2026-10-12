using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Api.Contracts.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);
