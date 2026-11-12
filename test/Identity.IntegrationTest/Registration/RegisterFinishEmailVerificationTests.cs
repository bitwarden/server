using System.ComponentModel.DataAnnotations;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using static Bit.Identity.IntegrationTest.Helpers.RegistrationTestHelpers;

namespace Bit.Identity.IntegrationTest.Registration;

public class RegisterFinishEmailVerificationTests
{
    [Theory, BitAutoData]
    // marketing emails can stay at top level
    public async Task RegistrationWithEmailVerification_WithEmailVerificationToken_Succeeds([Required] string name, bool receiveMarketingEmails,
         [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
         [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        // we must first call the send verification email endpoint to trigger the first part of the process
        var email = $"test+register+{name}@email.com";
        var sendVerificationEmailReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var sendEmailVerificationResponseHttpContext = await localFactory.PostRegisterSendEmailVerificationAsync(sendVerificationEmailReqModel);

        Assert.Equal(StatusCodes.Status204NoContent, sendEmailVerificationResponseHttpContext.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
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
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
        Assert.True(user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithEnableEmailVerificationFalse_DoesNotMarkEmailVerified(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // With email verification disabled, the token comes back in the response body and no email
        // is sent, so registering with it proves nothing about mailbox ownership.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", "false");

        var email = $"test+register+noemail+{name}@email.com";
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status200OK, sendCtx.Response.StatusCode);
        var emailVerificationToken = await sendCtx.ReadBodyAsStringAsync();
        Assert.False(localFactory.RegistrationTokens.ContainsKey(email));

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(name, user.Name);
        Assert.False(user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_TokenIssuedWithEnableEmailVerificationFalse_FinishedAfterSettingEnabled_DoesNotMarkEmailVerified(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // Whether the email is verified depends on how the token was delivered, not on the
        // setting's value when registration finishes.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", "false");

        var email = $"test+register+settingflip+{name}@email.com";
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status200OK, sendCtx.Response.StatusCode);
        var emailVerificationToken = await sendCtx.ReadBodyAsStringAsync();

        // GlobalSettings is a singleton, so this changes the value every command reads from here on.
        localFactory.GetService<GlobalSettings>().EnableEmailVerification = true;

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.False(user.EmailVerified);
    }

    // TODO: PM-45479 - Delete this test.
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task RegistrationWithEmailVerification_TokenWithoutEmailSent_MarksEmailVerifiedPerEnableEmailVerification(
        bool enableEmailVerification,
        [Required] string name,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // A token issued before EmailSent existed was emailed only when email verification was enabled.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", enableEmailVerification.ToString());

        var email = $"test+register+noemailsent+{name}@email.com";
        var emailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(localFactory, email, name);

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(name, user.Name);
        Assert.Equal(enableEmailVerification, user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_OpenRegistrationDisabled_ThrowsBadRequestException([Required] string name, string emailVerificationToken,
       [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
       [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+register+{name}@email.com";

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }
}
