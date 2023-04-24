using System.Diagnostics.CodeAnalysis;
using Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Tools.Repositories.EqualityComparers;

public class SendCompare : IEqualityComparer<Send>
{
    public bool Equals(Send x, Send y)
    {
        return x.Type == y.Type &&
        x.Data == y.Data &&
        x.Key == y.Key &&
        x.Password == y.Password &&
        x.MaxAccessCount == y.MaxAccessCount &&
        x.AccessCount == y.AccessCount &&
        x.ExpirationDate?.ToShortDateString() == y.ExpirationDate?.ToShortDateString() &&
        x.DeletionDate.ToShortDateString() == y.DeletionDate.ToShortDateString() &&
        x.Disabled == y.Disabled &&
        x.HideEmail == y.HideEmail;
    }

    public int GetHashCode([DisallowNull] Send obj)
    {
        return base.GetHashCode();
    }
}
