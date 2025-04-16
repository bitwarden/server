using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Models.Data.Organizations;

public record ClaimedUserDomainClaimedEmails(IEnumerable<string> EmailList, Organization Organization);
