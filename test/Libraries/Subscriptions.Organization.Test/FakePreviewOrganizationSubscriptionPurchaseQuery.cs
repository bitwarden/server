using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.Organization.Test;

internal sealed class FakePreviewOrganizationSubscriptionPurchaseQuery : IPreviewOrganizationSubscriptionPurchaseQuery
{
    public InvoicePreview? Result { get; init; }
    public Exception? Exception { get; init; }
    public UserEntity? ReceivedUser { get; private set; }
    public PreviewOrganizationSubscriptionPurchaseRequest? ReceivedRequest { get; private set; }
    public int Calls { get; private set; }

    public Task<InvoicePreview> Run(UserEntity user, PreviewOrganizationSubscriptionPurchaseRequest request)
    {
        Calls++;
        ReceivedUser = user;
        ReceivedRequest = request;
        return Exception is null ? Task.FromResult(Result!) : Task.FromException<InvoicePreview>(Exception);
    }
}
