using AutoFixture;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

public class OrganizationSignUpCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_WhenValidSignupAndFeatureFlagOff_ReturnsOrganizationAndOrganizationUser(
        OrganizationSignup signup,
        bool provider)
    {
        // Arrange
        var fixture = new Fixture();
        var paymentService = Substitute.For<IPaymentService>();
        var currentContext = Substitute.For<ICurrentContext>();
        var organizationService = Substitute.For<IOrganizationService>();
        var featureService = Substitute.For<IFeatureService>();
        var policyService = Substitute.For<IPolicyService>();
        var referenceEventService = Substitute.For<IReferenceEventService>();
        var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        var organizationSignUpValidationStrategy = Substitute.For<IOrganizationSignUpValidationStrategy>();

        var sut = new OrganizationSignUpCommand(
            paymentService,
            currentContext,
            organizationService,
            featureService,
            policyService,
            referenceEventService,
            organizationUserRepository,
            organizationSignUpValidationStrategy
        );

        var plans = fixture.Create<List<Plan>>();

        var passwordManagerPlan = fixture.Create<Organization>();
        var organization = fixture.Create<Organization>();
        var organizationUser = fixture.Create<OrganizationUser>();

        featureService.IsEnabled("sm-ga-billing", Arg.Any<CurrentContext>()).Returns(true);

        policyService.AnyPoliciesApplicableToUserAsync(signup.Owner.Id, PolicyType.SingleOrg).Returns(false);
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id).Returns(0);
        organizationService.SignUpAsync(Arg.Any<Organization>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), true)
            .Returns(new Tuple<Organization, OrganizationUser>(organization, organizationUser));

        fixture.Inject(plans);
        fixture.Inject(passwordManagerPlan);
        fixture.Inject(organization);
        fixture.Inject(organizationUser);
        fixture.Inject(featureService);
        fixture.Inject(policyService);
        fixture.Inject(organizationService);
        fixture.Inject(paymentService);
        fixture.Inject(referenceEventService);
        fixture.Inject(organizationUserRepository);
        fixture.Inject(organizationSignUpValidationStrategy);

        // Act
        signup.AdditionalStorageGb = 0;
        signup.AdditionalSeats = 0;
        var result = await sut.SignUpAsync(signup, provider);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organization, result.Item1);
        Assert.Equal(organizationUser, result.Item2);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_WhenValidSignupAndFeatureFlagOn_ReturnsOrganizationAndOrganizationUser(
        OrganizationSignup signup,
        bool provider)
    {
        // Arrange
        var fixture = new Fixture();
        var paymentService = Substitute.For<IPaymentService>();
        var currentContext = Substitute.For<ICurrentContext>();
        var organizationService = Substitute.For<IOrganizationService>();
        var featureService = Substitute.For<IFeatureService>();
        var policyService = Substitute.For<IPolicyService>();
        var referenceEventService = Substitute.For<IReferenceEventService>();
        var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        var organizationSignUpValidationStrategy = Substitute.For<IOrganizationSignUpValidationStrategy>();

        var sut = new OrganizationSignUpCommand(
            paymentService,
            currentContext,
            organizationService,
            featureService,
            policyService,
            referenceEventService,
            organizationUserRepository,
            organizationSignUpValidationStrategy
        );

        var plans = fixture.Create<List<Plan>>();

        var passwordManagerPlan = fixture.Create<Organization>();
        var organization = fixture.Create<Organization>();
        var organizationUser = fixture.Create<OrganizationUser>();

        featureService.IsEnabled("SecretManagerGaBilling", Arg.Any<CurrentContext>()).Returns(true);
        policyService.AnyPoliciesApplicableToUserAsync(signup.Owner.Id, PolicyType.SingleOrg).Returns(false);
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id).Returns(0);
        organizationService.SignUpAsync(Arg.Any<Organization>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), true)
            .Returns(new Tuple<Organization, OrganizationUser>(organization, organizationUser));

        fixture.Inject(plans);
        fixture.Inject(passwordManagerPlan);
        fixture.Inject(organization);
        fixture.Inject(organizationUser);
        fixture.Inject(featureService);
        fixture.Inject(policyService);
        fixture.Inject(organizationService);
        fixture.Inject(paymentService);
        fixture.Inject(referenceEventService);
        fixture.Inject(organizationUserRepository);
        fixture.Inject(organizationSignUpValidationStrategy);

        // Act
        signup.AdditionalStorageGb = 0;
        signup.AdditionalSeats = 0;
        signup.UseSecretsManager = true;
        signup.AdditionalServiceAccount = 0;
        signup.AdditionalSmSeats = 0;
        var result = await sut.SignUpAsync(signup, provider);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organization, result.Item1);
        Assert.Equal(organizationUser, result.Item2);
    }
}
