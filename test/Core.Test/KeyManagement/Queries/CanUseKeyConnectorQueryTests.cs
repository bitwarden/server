using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Queries;

[SutProviderCustomize]
public class CanUseKeyConnectorQueryTests
{
    [Theory]
    [BitAutoData]
    public void VerifyCanUseKeyConnector_UserAlreadyUsesKeyConnector_ThrowsBadRequestException(
        SutProvider<CanUseKeyConnectorQuery> sutProvider,
        User user)
    {
        // Arrange
        user.UsesKeyConnector = true;
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>());

        // Act & Assert
        var exception = Assert.Throws<BadRequestException>(() =>
            sutProvider.Sut.VerifyCanUseKeyConnector(user));

        Assert.Equal("Already uses Key Connector.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void VerifyCanUseKeyConnector_UserIsOwnerOfOrganization_ThrowsBadRequestException(
        SutProvider<CanUseKeyConnectorQuery> sutProvider,
        User user)
    {
        // Arrange
        user.UsesKeyConnector = false;
        var organizations = new List<CurrentContextOrganization>
        {
            new CurrentContextOrganization
            {
                Id = Guid.NewGuid(),
                Type = OrganizationUserType.Owner
            }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(organizations);

        // Act & Assert
        var exception = Assert.Throws<BadRequestException>(() =>
            sutProvider.Sut.VerifyCanUseKeyConnector(user));

        Assert.Equal("Cannot use Key Connector when admin or owner of an organization.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void VerifyCanUseKeyConnector_UserIsAdminOfOrganization_ThrowsBadRequestException(
        SutProvider<CanUseKeyConnectorQuery> sutProvider,
        User user)
    {
        // Arrange
        user.UsesKeyConnector = false;
        var organizations = new List<CurrentContextOrganization>
        {
            new CurrentContextOrganization
            {
                Id = Guid.NewGuid(),
                Type = OrganizationUserType.Admin
            }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(organizations);

        // Act & Assert
        var exception = Assert.Throws<BadRequestException>(() =>
            sutProvider.Sut.VerifyCanUseKeyConnector(user));

        Assert.Equal("Cannot use Key Connector when admin or owner of an organization.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void VerifyCanUseKeyConnector_UserIsNotOwnerOrAdmin_Success(
        SutProvider<CanUseKeyConnectorQuery> sutProvider,
        User user)
    {
        // Arrange
        user.UsesKeyConnector = false;
        var organizations = new List<CurrentContextOrganization>
        {
            new CurrentContextOrganization
            {
                Id = Guid.NewGuid(),
                Type = OrganizationUserType.User
            }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(organizations);

        // Act - should not throw
        sutProvider.Sut.VerifyCanUseKeyConnector(user);
    }
}
