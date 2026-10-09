using System.Diagnostics;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

public class ConfirmOrganizationInviteLinkErrorMapperTests
{
    public static TheoryData<Error, Type> Mappings() => new()
    {
        { new InviteLinkNotAvailable(), typeof(ConfirmInviteLinkNotAvailable) },
        { new EmailDomainNotAllowed("Org"), typeof(ConfirmEmailDomainNotAllowed) },
        { new EmailNotVerified(), typeof(ConfirmEmailNotVerified) },
        { new OnlyOneFreeOrganizationAdminAllowed(), typeof(ConfirmOnlyOneFreeOrganizationAdminAllowed) },
        { new OrganizationHasNoAvailableSeats("Org"), typeof(ConfirmOrganizationHasNoAvailableSeats) },
        { new UserIsAMemberOfAnotherOrganization(), typeof(ConfirmUserIsAMemberOfAnotherOrganization) },
        { new UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy(), typeof(ConfirmUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy) },
        { new TwoFactorRequiredForMembership(), typeof(ConfirmTwoFactorRequiredForMembership) },
        { new OtherOrganizationDoesNotAllowOtherMembership("user@example.com"), typeof(ConfirmOtherOrganizationDoesNotAllowOtherMembership) },
        { new UserCannotBelongToAnotherOrganization("user@example.com"), typeof(ConfirmUserCannotBelongToAnotherOrganization) },
        { new ProviderUsersCannotAcceptInviteLink(), typeof(ConfirmProviderUsersCannotAcceptInviteLink) },
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void ToValidationError_MapsToValidationErrorVariantWithSameMessage(Error error, Type expectedType)
    {
        // Act
        var mapped = ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(error);

        // Assert
        Assert.IsType(expectedType, mapped);
        Assert.IsAssignableFrom<IValidationError>(mapped);
        Assert.Equal(error.Message, mapped.Message);
    }

    [Fact]
    public void ToValidationError_InviteLinkNotFound_IsReturnedUnchanged()
    {
        // Arrange
        var error = new InviteLinkNotFound();

        // Act
        var mapped = ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(error);

        // Assert
        Assert.Same(error, mapped);
    }

    [Fact]
    public void ToValidationError_AlreadyAValidationError_IsReturnedUnchanged()
    {
        // Arrange
        var error = new ConfirmEmailNotVerified();

        // Act
        var mapped = ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(error);

        // Assert
        Assert.Same(error, mapped);
    }

    [Fact]
    public void ToValidationError_UnmappedError_Throws()
    {
        // Arrange
        var error = new UnmappedError();

        // Act & Assert
        Assert.Throws<UnreachableException>(() => ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(error));
    }

    private record UnmappedError() : BadRequestError("Unmapped error.");
}
