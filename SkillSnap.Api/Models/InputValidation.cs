using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Api.Models;

/// <summary>Rejects strings that are empty or only whitespace; null is left to [Required] so optional fields still work.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class NotWhitespaceAttribute : ValidationAttribute
{
    public NotWhitespaceAttribute() : base("The {0} field cannot be blank.")
    {
    }

    public override bool IsValid(object? value) => value is not string text || !string.IsNullOrWhiteSpace(text);
}

/*
/// <summary>Rejects order line lists that name the same item more than once, so each item appears on one line.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class UniqueItemsAttribute : ValidationAttribute
{
    public UniqueItemsAttribute() : base("Each item can appear only once; combine duplicate lines into one quantity.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not IEnumerable<CreateOrderLineRequest> lines || lines.Select(line => line.ItemId).Distinct().Count() == lines.Count();
}
*/

/// <summary>Upper bounds shared by the request models.</summary>
public static class InputLimits
{
    public const int MaxOrderLineQuantity = 100_000;
    public const int MaxStockQuantity = 10_000_000;
    public const int MaxEmailLength = 256;
    public const int MaxPasswordLength = 128;
    public const int MaxTokenLength = 200;
    public const int MaxRoleLength = 50;
    public const int MaxUserIdLength = 450;
}