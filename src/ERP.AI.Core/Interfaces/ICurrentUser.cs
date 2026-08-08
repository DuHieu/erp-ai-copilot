namespace ERP.AI.Core.Interfaces;

public interface ICurrentUser
{
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
}
