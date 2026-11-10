using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers.Accounts;

public class RegisterVerificationEmailClickedTests
{
    [Theory, BitAutoData]
    public async Task PostRegisterVerificationEmailClicked_Success(
        [Required, StringLength(20)] string name,
        string emailVerificationToken)
    {
        // Arrange
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        var email = $"test+register+{name}@email.com";
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email);

        localFactory.SubstituteService<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>(emailVerificationTokenDataProtectorFactory =>
        {
            emailVerificationTokenDataProtectorFactory.TryUnprotect(Arg.Is(emailVerificationToken), out Arg.Any<RegistrationEmailVerificationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = registrationEmailVerificationTokenable;
                    return true;
                });
        });

        var requestModel = new RegisterVerificationEmailClickedRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken
        };

        // Act
        var httpContext = await localFactory.PostRegisterVerificationEmailClicked(requestModel);

        var body = await httpContext.ReadBodyAsStringAsync();

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }
}
