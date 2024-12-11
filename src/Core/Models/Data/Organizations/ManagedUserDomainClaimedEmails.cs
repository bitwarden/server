using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Models.Data.Organizations;

public record ManagedUserDomainClaimedEmails(IEnumerable<string> EmailList, Organization Organization);
