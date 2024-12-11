using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class TransactionCompare : IEqualityComparer<Transaction>
{
    public bool Equals(Transaction x, Transaction y)
    {
        return x.Type == y.Type
            && x.Amount == y.Amount
            && x.Refunded == y.Refunded
            && x.Details == y.Details
            && x.PaymentMethodType == y.PaymentMethodType
            && x.Gateway == y.Gateway
            && x.GatewayId == y.GatewayId;
    }

    public int GetHashCode([DisallowNull] Transaction obj)
    {
        return base.GetHashCode();
    }
}
