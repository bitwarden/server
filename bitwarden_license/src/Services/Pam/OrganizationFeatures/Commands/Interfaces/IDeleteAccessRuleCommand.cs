namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDeleteAccessRuleCommand
{
    Task DeleteAsync(Guid organizationId, Guid id, Guid? deletedBy);
}
