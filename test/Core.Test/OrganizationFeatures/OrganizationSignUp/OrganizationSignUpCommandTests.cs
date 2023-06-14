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
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

[SutProviderCustomize]
public class OrganizationSignUpCommandTests
{
    [Theory, BitAutoData]
    public async Task SignUpAsync_WhenValidSignup_ReturnsOrganizationAndOrganizationUser(
        SutProvider<OrganizationSignUpCommand> sutProvider, OrganizationSignup signup,
        bool provider)
    {
        var fixture = new Fixture();
        var paymentService = sutProvider.GetDependency<IPaymentService>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var referenceEventService = sutProvider.GetDependency<IReferenceEventService>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationSignUpValidationStrategy = sutProvider.GetDependency<IOrganizationSignUpValidationStrategy>();

        var plans = fixture.Create<List<Plan>>();

        var passwordManagerPlan = fixture.Create<Organization>();
        var organization = fixture.Create<Organization>();
        var organizationUser = fixture.Create<OrganizationUser>();

        policyService.AnyPoliciesApplicableToUserAsync(signup.Owner.Id, PolicyType.SingleOrg).Returns(false);
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id).Returns(0);
        organizationService.SignUpAsync(Arg.Any<Organization>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), true)
            .Returns(new Tuple<Organization, OrganizationUser>(organization, organizationUser));

        fixture.Inject(plans);
        fixture.Inject(passwordManagerPlan);
        fixture.Inject(organization);
        fixture.Inject(organizationUser);
        fixture.Inject(policyService);
        fixture.Inject(organizationService);
        fixture.Inject(paymentService);
        fixture.Inject(referenceEventService);
        fixture.Inject(organizationUserRepository);
        fixture.Inject(organizationSignUpValidationStrategy);

        signup.AdditionalStorageGb = 0;
        signup.AdditionalSeats = 0;
        signup.UseSecretsManager = true;
        signup.AdditionalServiceAccount = 0;
        signup.AdditionalSmSeats = 0;
        var result = await sutProvider.Sut.SignUpAsync(signup, provider);

        Assert.NotNull(result);
        Assert.Equal(organization, result.Item1);
        Assert.Equal(organizationUser, result.Item2);
    }

}
