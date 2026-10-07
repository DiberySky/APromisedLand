namespace TreeGraph.Shared.NodeEav.Dtos;

public record ValidationError(string Field, string Message);

public record ValidationResult(bool IsValid, List<ValidationError> Errors);
