using System.ComponentModel.DataAnnotations;
using APromisedLand.Shared.Validators;

namespace APromisedLand.Shared.DTOs.Overflow;

public record CreateQuestionDto(
    [Required] string Title,
    [Required] string Content,
    [Required] [TagListValidator(1, 5)] List<string> Tags
);