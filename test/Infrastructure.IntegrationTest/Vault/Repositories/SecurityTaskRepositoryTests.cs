using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Vault.Repositories;

public class SecurityTaskRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync(
        IOrganizationRepository organizationRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository
    )
    {
        var organization = await organizationRepository.CreateAsync(
            new Organization
            {
                Name = "Test Org",
                PlanType = PlanType.EnterpriseAnnually,
                Plan = "Test Plan",
                BillingEmail = "billing@email.com",
            }
        );

        var cipher = await cipherRepository.CreateAsync(
            new Cipher
            {
                Type = CipherType.Login,
                OrganizationId = organization.Id,
                Data = "",
            }
        );

        var task = await securityTaskRepository.CreateAsync(
            new SecurityTask
            {
                OrganizationId = organization.Id,
                CipherId = cipher.Id,
                Status = SecurityTaskStatus.Pending,
                Type = SecurityTaskType.UpdateAtRiskCredential,
            }
        );

        Assert.NotNull(task);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReadByIdAsync(
        IOrganizationRepository organizationRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository
    )
    {
        var organization = await organizationRepository.CreateAsync(
            new Organization
            {
                Name = "Test Org",
                PlanType = PlanType.EnterpriseAnnually,
                Plan = "Test Plan",
                BillingEmail = "billing@email.com",
            }
        );

        var cipher = await cipherRepository.CreateAsync(
            new Cipher
            {
                Type = CipherType.Login,
                OrganizationId = organization.Id,
                Data = "",
            }
        );

        var task = await securityTaskRepository.CreateAsync(
            new SecurityTask
            {
                OrganizationId = organization.Id,
                CipherId = cipher.Id,
                Status = SecurityTaskStatus.Pending,
                Type = SecurityTaskType.UpdateAtRiskCredential,
            }
        );

        Assert.NotNull(task);

        var readTask = await securityTaskRepository.GetByIdAsync(task.Id);

        Assert.NotNull(readTask);
        Assert.Equal(task.Id, readTask.Id);
        Assert.Equal(task.Status, readTask.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateAsync(
        IOrganizationRepository organizationRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository
    )
    {
        var organization = await organizationRepository.CreateAsync(
            new Organization
            {
                Name = "Test Org",
                PlanType = PlanType.EnterpriseAnnually,
                Plan = "Test Plan",
                BillingEmail = "billing@email.com",
            }
        );

        var cipher = await cipherRepository.CreateAsync(
            new Cipher
            {
                Type = CipherType.Login,
                OrganizationId = organization.Id,
                Data = "",
            }
        );

        var task = await securityTaskRepository.CreateAsync(
            new SecurityTask
            {
                OrganizationId = organization.Id,
                CipherId = cipher.Id,
                Status = SecurityTaskStatus.Pending,
                Type = SecurityTaskType.UpdateAtRiskCredential,
            }
        );

        Assert.NotNull(task);

        task.Status = SecurityTaskStatus.Completed;
        await securityTaskRepository.ReplaceAsync(task);

        var updatedTask = await securityTaskRepository.GetByIdAsync(task.Id);

        Assert.NotNull(updatedTask);
        Assert.Equal(task.Id, updatedTask.Id);
        Assert.Equal(SecurityTaskStatus.Completed, updatedTask.Status);
    }
}
