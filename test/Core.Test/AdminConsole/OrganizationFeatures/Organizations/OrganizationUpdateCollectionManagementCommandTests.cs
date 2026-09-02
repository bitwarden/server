using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationUpdateCollectionManagementCommandTests
{
    [Theory]
    [BitAutoData(false, true, false, true)]
    [BitAutoData(true, false, true, false)]
    public async Task UpdateAsync_WhenSettingsChanged_LogsSpecificEvents(
        bool newLimitCollectionCreation,
        bool newLimitCollectionDeletion,
        bool newLimitItemDeletion,
        bool newAllowAdminAccessToAllCollectionItems,
        Organization existingOrganization, SutProvider<OrganizationUpdateCollectionManagementCommand> sutProvider)
    {
        // Arrange
        existingOrganization.LimitCollectionCreation = false;
        existingOrganization.LimitCollectionDeletion = false;
        existingOrganization.LimitItemDeletion = false;
        existingOrganization.AllowAdminAccessToAllCollectionItems = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(existingOrganization.Id)
            .Returns(existingOrganization);

        var settings = new OrganizationCollectionManagementSettings
        {
            LimitCollectionCreation = newLimitCollectionCreation,
            LimitCollectionDeletion = newLimitCollectionDeletion,
            LimitItemDeletion = newLimitItemDeletion,
            AllowAdminAccessToAllCollectionItems = newAllowAdminAccessToAllCollectionItems
        };

        // Act
        await sutProvider.Sut.UpdateAsync(existingOrganization.Id, settings);

        // Assert
        var eventService = sutProvider.GetDependency<IEventService>();
        if (newLimitCollectionCreation)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionCreationEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionCreationEnabled));
        }

        if (newLimitCollectionDeletion)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionDeletionEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionDeletionEnabled));
        }

        if (newLimitItemDeletion)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitItemDeletionEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitItemDeletionEnabled));
        }

        if (newAllowAdminAccessToAllCollectionItems)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_AllowAdminAccessToAllCollectionItemsEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_AllowAdminAccessToAllCollectionItemsEnabled));
        }
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenOrganizationNotFound_ThrowsNotFoundException(
        Guid organizationId, OrganizationCollectionManagementSettings settings, SutProvider<OrganizationUpdateCollectionManagementCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        // Act/Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(organizationId, settings));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organizationId);
    }
}
