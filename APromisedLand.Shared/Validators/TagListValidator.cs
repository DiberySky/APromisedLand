using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace APromisedLand.Shared.Validators;

public class TagListValidator(int min, int max) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is List<string> tags)
        {
            if (tags.Count >= min && tags.Count <= max) return ValidationResult.Success;
        }
        
        return new ValidationResult($"你必须提供至少 {min}，最多 {max} 标签。");
    }
}