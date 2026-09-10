using Microsoft.Extensions.Options;

namespace MAFRagService.Startup.Configuration;

public sealed class OllamaOptionsValidator : IValidateOptions<OllamaOptions>
{
    public ValidateOptionsResult Validate(string? name, OllamaOptions opt)
    {
        var errors = new List<string>();

        if (opt.PolicyTimeout >= opt.RequestTimeout)
            errors.Add(
                $"OllamaOptions.PolicyTimeout({opt.PolicyTimeout}) 必须小于 RequestTimeout({opt.RequestTimeout})，" +
                "否则 Polly 超时策略不会生效。");

        if (opt.ChatModel.Equals(opt.EmbeddingModel, StringComparison.OrdinalIgnoreCase))
            errors.Add("OllamaOptions.ChatModel 与 EmbeddingModel 不应相同。");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}