namespace ERP.AI.Core.Interfaces;

public interface IErpTool
{
    string Name { get; }
    string Description { get; }
    string RequiredPermission { get; }
    string ParameterJsonSchema { get; }
    Task<object> ExecuteAsync(string jsonArguments, CancellationToken cancellationToken = default);
}
