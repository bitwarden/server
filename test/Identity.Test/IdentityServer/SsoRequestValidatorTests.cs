using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidationConstants;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;

namespace Bit.Identity.Test.IdentityServer;

[SutProviderCustomize]
public class SsoRequestValidatorTests
{
    [Theory]
    [BitAutoData(OidcConstants.GrantTypes.AuthorizationCode)]
    [BitAutoData(OidcConstants.GrantTypes.ClientCredentials)]
    public async void ValidateAsync_GrantTypeIgnoresSsoRequirement_ReturnsTrue(
        string grantType,
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = grantType;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired);
        Assert.Null(context.ValidationErrorResult);
        Assert.Null(context.CustomResponse);

        // Should not check policies or organizations since grant type allows bypass
        await sutProvider.GetDependency<IPolicyRequirementQuery>().DidNotReceive()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoNotRequired_ReturnsTrue(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired);
        Assert.Null(context.ValidationErrorResult);
        Assert.Null(context.CustomResponse);

        await sutProvider.GetDependency<IPolicyRequirementQuery>().Received(1).GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_NoSsoOrganizationIds_ReturnsFalse_OmitsIdentifier(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = Array.Empty<Guid>()
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.ValidationErrorResult);
        Assert.True(context.ValidationErrorResult.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.ValidationErrorResult.Error);
        Assert.Equal(SsoConstants.RequestErrors.SsoRequiredDescription, context.ValidationErrorResult.ErrorDescription);

        Assert.NotNull(context.CustomResponse);
        Assert.True(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.ErrorModel));
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_TwoFactorRecoveryRequested_ReturnsFalse_WithSpecialMessage(
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.TwoFactorRecoveryRequested = true;
        context.TwoFactorRequired = true;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.ValidationErrorResult);
        Assert.True(context.ValidationErrorResult.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.ValidationErrorResult.Error);
        Assert.Equal(SsoConstants.RequestErrors.SsoTwoFactorRecoveryDescription,
            context.ValidationErrorResult.ErrorDescription);

        Assert.NotNull(context.CustomResponse);
        Assert.True(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.ErrorModel));
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_TwoFactorRequiredButNotRecovery_ReturnsFalse_WithStandardMessage(
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.ValidationErrorResult);
        Assert.True(context.ValidationErrorResult.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.ValidationErrorResult.Error);
        Assert.Equal(SsoConstants.RequestErrors.SsoRequiredDescription, context.ValidationErrorResult.ErrorDescription);

        Assert.NotNull(context.CustomResponse);
        Assert.True(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.ErrorModel));
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));
    }

    [Theory]
    [BitAutoData(OidcConstants.GrantTypes.Password)]
    [BitAutoData(OidcConstants.GrantTypes.RefreshToken)]
    [BitAutoData(CustomGrantTypes.WebAuthn)]
    public async void ValidateAsync_VariousGrantTypes_SsoRequired_ReturnsFalse(
        string grantType,
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = grantType;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.ValidationErrorResult);
        Assert.True(context.ValidationErrorResult.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.ValidationErrorResult.Error);
        Assert.Equal(SsoConstants.RequestErrors.SsoRequiredDescription, context.ValidationErrorResult.ErrorDescription);
        Assert.NotNull(context.CustomResponse);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_ContextSsoRequiredUpdated_RegardlessOfInitialValue(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.SsoRequired = true; // Start with true to ensure it gets updated

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired); // Should be updated to false
        Assert.Null(context.ValidationErrorResult);
        Assert.Null(context.CustomResponse);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_SingleOrgWithIdentifier_IncludesIdentifierInResponse(
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        const string orgIdentifier = "test-organization";
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Identifier = orgIdentifier });

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.True(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));
        Assert.Equal(orgIdentifier, context.CustomResponse[CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier]);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_OrganizationHasNullIdentifier_OmitsIdentifierFromResponse(
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Identifier = null });

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_OrganizationHasEmptyIdentifier_OmitsIdentifierFromResponse(
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Identifier = string.Empty });

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_MultipleSsoOrganizationIds_OmitsIdentifier(
        User user,
        Guid orgIdA,
        Guid orgIdB,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { orgIdA, orgIdB }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        // With ambiguous SSO membership, we don't pick an arbitrary org's identifier.
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_OrganizationNotFound_OmitsIdentifier(
        User user,
        Guid organizationId,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        var requirement = new RequireSsoPolicyRequirement
        {
            SsoRequired = true,
            SsoOrganizationIds = new[] { organizationId }
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoNotRequired_DoesNotCallOrganizationRepository(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired);

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }
}
