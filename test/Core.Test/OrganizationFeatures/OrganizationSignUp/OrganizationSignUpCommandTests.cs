using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

public class OrganizationSignUpCommandTests
{
    [Fact]
    public async Task SignUpAsync_Should_ReturnOrganizationAndUser()
    {
        // Arrange
        var organizationService = Substitute.For<IOrganizationService>();
        var paymentService = Substitute.For<IPaymentService>();
        var referenceEventService = Substitute.For<IReferenceEventService>();
        var featureService = Substitute.For<IFeatureService>();
        var policyService = Substitute.For<IPolicyService>();
        var currentContext = Substitute.For<ICurrentContext>();
        var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        var organizationSignUpValidationStrategy = Substitute.For<IOrganizationSignUpValidationStrategy>();
    
        var sut = new OrganizationSignUpCommand(paymentService, currentContext, organizationService, featureService
            , policyService, referenceEventService, organizationUserRepository, organizationSignUpValidationStrategy);
    
        var signup = new OrganizationSignup { /* provide necessary properties */ };
        var provider = false;
        var passwordManagerPlan = new Plan { /* provide necessary properties */ };
        var secretsManagerPlan = new Plan { /* provide necessary properties */ };
        var plans = new List<Plan> { passwordManagerPlan, secretsManagerPlan };
        var organization = new Organization { /* provide necessary properties */ };
        var organizationUser = new OrganizationUser { /* provide necessary properties */ };
    
        organizationService.SignUpAsync(Arg.Any<Organization>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(organization, organizationUser)));
    
        // Act
        var result = await sut.SignUpAsync(signup, provider);
    
        // Assert
        Assert.Equal(organization, result.Item1);
        Assert.Equal(organizationUser, result.Item2);
    }
}
