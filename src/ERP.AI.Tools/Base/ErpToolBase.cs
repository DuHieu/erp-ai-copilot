using System.Text.Json;
using ERP.AI.Core.Interfaces;

namespace ERP.AI.Tools.Base;

public abstract class ErpToolBase<TInput, TOutput> : IErpTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string RequiredPermission { get; }
    public abstract string ParameterJsonSchema { get; }

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<object> ExecuteAsync(string jsonArguments, CancellationToken cancellationToken = default)
    {
        TInput input = DeserializeInput(jsonArguments);
        ValidateInput(input);
        TOutput result = await ExecuteCoreAsync(input, cancellationToken);
        return result!;
    }

    protected virtual TInput DeserializeInput(string jsonArguments)
    {
        if (string.IsNullOrWhiteSpace(jsonArguments) || jsonArguments.Trim() == "{}" || typeof(TInput) == typeof(EmptyInput))
        {
            return (TInput)Activator.CreateInstance(typeof(TInput))!;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TInput>(jsonArguments, JsonOptions);
            return parsed ?? (TInput)Activator.CreateInstance(typeof(TInput))!;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid parameter format for tool '{Name}': {ex.Message}", ex);
        }
    }

    protected virtual void ValidateInput(TInput input)
    {
        // Default no-op, override in child tools if needed
    }

    protected abstract Task<TOutput> ExecuteCoreAsync(TInput input, CancellationToken cancellationToken);
}

public class EmptyInput
{
}
