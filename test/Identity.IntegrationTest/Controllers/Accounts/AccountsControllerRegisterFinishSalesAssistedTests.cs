using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Tokens;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers.Accounts;

public class AccountsControllerRegisterFinishSalesAssistedTests
{
    [Theory, BitAutoData]
    public async Task RegisterViaSalesAssistedToken_WithDisabledRegistration_Succeeds(
        string name,
        [StringLength(1000)] string masterPasswordHash,
        [StringLength(50)] string masterPasswordHint,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        int kdfMemory,
        int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+sales+{name}@email.com";
        const string salesAssistedToken = "fake-sales-assisted-token";

        var tokenable = new SalesAssistedRegistrationTokenable
        {
            Email = email,
            Name = name,
            ExpirationDate = DateTime.UtcNow.AddDays(5)
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(factory =>
        {
            factory.TryUnprotect(Arg.Is(salesAssistedToken), out Arg.Any<SalesAssistedRegistrationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = tokenable;
                    return true;
                });
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            SalesAssistedToken = salesAssistedToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);
        Assert.NotNull(user.MasterPassword);
    }

    [Theory, BitAutoData]
    public async Task RegisterViaSalesAssistedToken_WithInvalidToken_Returns400(
        string name,
        [StringLength(1000)] string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        userAsymmetricKeys.AccountKeys = null;

        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+sales+{name}@email.com";
        const string salesAssistedToken = "invalid-token";

        localFactory.SubstituteService<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(factory =>
        {
            factory.TryUnprotect(Arg.Any<string>(), out Arg.Any<SalesAssistedRegistrationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = null;
                    return false;
                });
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            SalesAssistedToken = salesAssistedToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegisterViaSalesAssistedToken_WithEmailMismatch_Returns400(
        string name,
        [StringLength(1000)] string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        userAsymmetricKeys.AccountKeys = null;

        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+sales+{name}@email.com";
        var tokenEmail = $"other+{name}@email.com";
        const string salesAssistedToken = "fake-sales-assisted-token";

        // Token is valid but bound to a different email address — TokenIsValid(email) must reject this.
        var tokenable = new SalesAssistedRegistrationTokenable
        {
            Email = tokenEmail,
            ExpirationDate = DateTime.UtcNow.AddDays(5)
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(factory =>
        {
            factory.TryUnprotect(Arg.Is(salesAssistedToken), out Arg.Any<SalesAssistedRegistrationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = tokenable;
                    return true;
                });
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,  // does not match tokenEmail
            MasterPasswordHash = masterPasswordHash,
            SalesAssistedToken = salesAssistedToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }
}
