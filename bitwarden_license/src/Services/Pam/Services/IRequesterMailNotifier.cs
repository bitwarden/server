using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>Emails a requester the verdict on their request. Never throws.</summary>
public interface IRequesterMailNotifier
{
    Task NotifyDecisionAsync(AccessRequest request, bool approved);
}
