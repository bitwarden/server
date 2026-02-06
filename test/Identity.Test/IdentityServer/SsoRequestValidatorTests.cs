using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Sso;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
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

        // Should not check policies since grant type allows bypass
        await sutProvider.GetDependency<IPolicyService>().DidNotReceive()
            .AnyPoliciesApplicableToUserAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IPolicyRequirementQuery>().DidNotReceive()
            .GetAsync<RequireSsoPolicyRequirement>(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoNotRequired_RequirementPolicyFeatureFlagEnabled_ReturnsTrue(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired);
        Assert.Null(context.ValidationErrorResult);
        Assert.Null(context.CustomResponse);

        // Should use the new policy requirement query when feature flag is enabled
        await sutProvider.GetDependency<IPolicyRequirementQuery>().Received(1).GetAsync<RequireSsoPolicyRequirement>(user.Id);
        await sutProvider.GetDependency<IPolicyService>().DidNotReceive()
            .AnyPoliciesApplicableToUserAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>(), Arg.Any<OrganizationUserStatusType>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoNotRequired_RequirementPolicyFeatureFlagDisabled_ReturnsTrue(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);

        sutProvider.GetDependency<IPolicyService>().AnyPoliciesApplicableToUserAsync(
            user.Id,
            PolicyType.RequireSso,
            OrganizationUserStatusType.Confirmed)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired);
        Assert.Null(context.ValidationErrorResult);
        Assert.Null(context.CustomResponse);

        // Should use the legacy policy service when feature flag is disabled
        await sutProvider.GetDependency<IPolicyService>().Received(1).AnyPoliciesApplicableToUserAsync(
            user.Id,
            PolicyType.RequireSso,
            OrganizationUserStatusType.Confirmed);
        await sutProvider.GetDependency<IPolicyRequirementQuery>().DidNotReceive()
            .GetAsync<RequireSsoPolicyRequirement>(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_RequirementPolicyFeatureFlagEnabled_ReturnsFalse(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns((string)null);

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

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_RequirementPolicyFeatureFlagDisabled_ReturnsFalse(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);

        sutProvider.GetDependency<IPolicyService>().AnyPoliciesApplicableToUserAsync(
            user.Id,
            PolicyType.RequireSso,
            OrganizationUserStatusType.Confirmed)
            .Returns(true);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns((string)null);

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
        Assert.True(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.False(context.CustomResponse.ContainsKey("SsoOrganizationIdentifier"));
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_TwoFactorRecoveryRequested_ReturnsFalse_WithSpecialMessage(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.TwoFactorRecoveryRequested = true;
        context.TwoFactorRequired = true;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns((string)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.ValidationErrorResult);
        Assert.True(context.ValidationErrorResult.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.ValidationErrorResult.Error);
        Assert.Equal("Two-factor recovery has been performed. SSO authentication is required.",
            context.ValidationErrorResult.ErrorDescription);

        Assert.NotNull(context.CustomResponse);
        Assert.True(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.False(context.CustomResponse.ContainsKey("SsoOrganizationIdentifier"));
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_TwoFactorRequiredButNotRecovery_ReturnsFalse_WithStandardMessage(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns((string)null);

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
        Assert.True(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.False(context.CustomResponse.ContainsKey("SsoOrganizationIdentifier"));
    }

    [Theory]
    [BitAutoData(OidcConstants.GrantTypes.Password)]
    [BitAutoData(OidcConstants.GrantTypes.RefreshToken)]
    [BitAutoData(CustomGrantTypes.WebAuthn)]
    public async void ValidateAsync_VariousGrantTypes_SsoRequired_ReturnsFalse(
        string grantType,
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = grantType;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns((string)null);

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

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
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
    public async void ValidateAsync_SsoRequired_WithOrganizationIdentifier_IncludesIdentifierInResponse(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        const string orgIdentifier = "test-organization";
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns(orgIdentifier);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.True(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));
        Assert.Equal(orgIdentifier, context.CustomResponse[CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier]);

        await sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .Received(1)
            .GetSsoOrganizationIdentifierAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_NoOrganizationIdentifier_DoesNotIncludeIdentifierInResponse(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns((string)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        await sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .Received(1)
            .GetSsoOrganizationIdentifierAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoRequired_EmptyOrganizationIdentifier_DoesNotIncludeIdentifierInResponse(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;
        context.User = user;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .GetSsoOrganizationIdentifierAsync(user.Id)
            .Returns(string.Empty);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.False(result);
        Assert.True(context.SsoRequired);
        Assert.NotNull(context.CustomResponse);
        Assert.False(context.CustomResponse.ContainsKey(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier));

        await sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .Received(1)
            .GetSsoOrganizationIdentifierAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async void ValidateAsync_SsoNotRequired_DoesNotCallOrganizationIdentifierQuery(
        User user,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        SutProvider<SsoRequestValidator> sutProvider)
    {
        // Arrange
        request.GrantType = OidcConstants.GrantTypes.Password;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<RequireSsoPolicyRequirement>(user.Id)
            .Returns(requirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, request, context);

        // Assert
        Assert.True(result);
        Assert.False(context.SsoRequired);

        await sutProvider.GetDependency<IUserSsoOrganizationIdentifierQuery>()
            .DidNotReceive()
            .GetSsoOrganizationIdentifierAsync(Arg.Any<Guid>());
    }
}
