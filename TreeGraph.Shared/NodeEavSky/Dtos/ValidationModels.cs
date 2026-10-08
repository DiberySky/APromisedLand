namespace TreeGraph.Shared.NodeEavSky.Dtos;

public record ValidationError(string Field, string Message);

public record ValidationResult(bool IsValid, List<ValidationError> Errors);
