using ERP.AI.Core.Interfaces;

namespace ERP.AI.Infrastructure.Security;

public class MockCurrentUser : ICurrentUser
{
    public string UserId => "demo-user";
    public IReadOnlyList<string> Roles => new List<string> { "Finance", "Manager" }.AsReadOnly();
}
