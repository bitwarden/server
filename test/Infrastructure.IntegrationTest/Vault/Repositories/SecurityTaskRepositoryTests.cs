using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Vault.Repositories;

public class SecurityTaskRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync(
        IOrganizationRepository organizationRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = "",
        });

        var task = await securityTaskRepository.CreateAsync(new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = cipher.Id,
            Status = SecurityTaskStatus.Pending,
            Type = SecurityTaskType.UpdateAtRiskCredential,
        });

        Assert.NotNull(task);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReadByIdAsync(
        IOrganizationRepository organizationRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = "",
        });

        var task = await securityTaskRepository.CreateAsync(new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = cipher.Id,
            Status = SecurityTaskStatus.Pending,
            Type = SecurityTaskType.UpdateAtRiskCredential,
        });

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
        ISecurityTaskRepository securityTaskRepository)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = "",
        });

        var task = await securityTaskRepository.CreateAsync(new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = cipher.Id,
            Status = SecurityTaskStatus.Pending,
            Type = SecurityTaskType.UpdateAtRiskCredential,
        });

        Assert.NotNull(task);

        task.Status = SecurityTaskStatus.Completed;
        await securityTaskRepository.ReplaceAsync(task);

        var updatedTask = await securityTaskRepository.GetByIdAsync(task.Id);

        Assert.NotNull(updatedTask);
        Assert.Equal(task.Id, updatedTask.Id);
        Assert.Equal(SecurityTaskStatus.Completed, updatedTask.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_ReturnsExpectedTasks(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed
        });

        var collection = await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = organization.Id,
            Name = "Test Collection",
        });

        var cipher1 = new Cipher { Type = CipherType.Login, OrganizationId = organization.Id, Data = "", };
        await cipherRepository.CreateAsync(cipher1, [collection.Id]);

        var cipher2 = new Cipher { Type = CipherType.Login, OrganizationId = organization.Id, Data = "", };
        await cipherRepository.CreateAsync(cipher2, [collection.Id]);

        var task1 = await securityTaskRepository.CreateAsync(new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = cipher1.Id,
            Status = SecurityTaskStatus.Pending,
            Type = SecurityTaskType.UpdateAtRiskCredential,
        });

        var task2 = await securityTaskRepository.CreateAsync(new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = cipher2.Id,
            Status = SecurityTaskStatus.Completed,
            Type = SecurityTaskType.UpdateAtRiskCredential,
        });

        var task3 = await securityTaskRepository.CreateAsync(new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = cipher2.Id,
            Status = SecurityTaskStatus.Pending,
            Type = SecurityTaskType.UpdateAtRiskCredential,
        });

        await collectionRepository.UpdateUsersAsync(collection.Id,
            new List<CollectionAccessSelection>
            {
                new() {Id = orgUser.Id, ReadOnly = false, HidePasswords = false, Manage = true}
            });

        var allTasks = await securityTaskRepository.GetManyByUserIdStatusAsync(user.Id);
        Assert.Contains(task1, allTasks, new SecurityTaskComparer());
        Assert.Contains(task2, allTasks, new SecurityTaskComparer());
        Assert.Contains(task3, allTasks, new SecurityTaskComparer());

        var pendingTasks = await securityTaskRepository.GetManyByUserIdStatusAsync(user.Id, [SecurityTaskStatus.Pending]);
        Assert.Contains(task1, pendingTasks, new SecurityTaskComparer());
        Assert.Contains(task3, pendingTasks, new SecurityTaskComparer());
        Assert.DoesNotContain(task2, pendingTasks, new SecurityTaskComparer());

        var completedTasks = await securityTaskRepository.GetManyByUserIdStatusAsync(user.Id, [SecurityTaskStatus.Completed]);
        Assert.Contains(task2, completedTasks, new SecurityTaskComparer());
        Assert.DoesNotContain(task1, completedTasks, new SecurityTaskComparer());
        Assert.DoesNotContain(task3, completedTasks, new SecurityTaskComparer());
    }
}
