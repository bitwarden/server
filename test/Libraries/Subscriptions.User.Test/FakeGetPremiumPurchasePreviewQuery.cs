using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Bit.Subscriptions.User.Queries;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test;

internal sealed class FakeGetPremiumPurchasePreviewQuery : IGetPremiumPurchasePreviewQuery
{
    public InvoicePreview? Result { get; init; }
    public Exception? Exception { get; init; }
    public UserEntity? ReceivedUser { get; private set; }
    public GetPremiumPurchasePreviewRequest? ReceivedRequest { get; private set; }
    public int Calls { get; private set; }

    public Task<InvoicePreview> Run(UserEntity user, GetPremiumPurchasePreviewRequest request)
    {
        Calls++;
        ReceivedUser = user;
        ReceivedRequest = request;
        return Exception is null ? Task.FromResult(Result!) : Task.FromException<InvoicePreview>(Exception);
    }
}
