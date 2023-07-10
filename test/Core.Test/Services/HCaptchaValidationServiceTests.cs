using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class HCaptchaValidationServiceTests
{
    public class CaptchaStateTestCase
    {
        public bool UserIsNull { get; set; }
        public bool IsBot { get; set; }
        public bool ForceCaptchaRequired { get; set; }
        public bool KnownDevice { get; set; }
        public bool UserIsNew { get; set; }
        public bool UserIsVerified { get; set; }
        public bool UserIsOnCloud { get; set; }
        public bool AtFailedLoginCeiling { get; set; }
        public bool ExpectedResult { get; set; }
    }

    public static IEnumerable<object[]> RequireCaptchaValidationTestCases => new CaptchaStateTestCase[][]
    {
        // Unknown Users
        new [] // is bot
        {
            new CaptchaStateTestCase
            {
                UserIsNull = true,
                IsBot = true,
                ForceCaptchaRequired = false,
                KnownDevice = false,
                UserIsNew = false,
                UserIsVerified = false,
                UserIsOnCloud = false,
                AtFailedLoginCeiling = false,
                ExpectedResult = true
            }
        },
        new [] // force captcha required
        {
            new CaptchaStateTestCase
            {
                UserIsNull = true,
                IsBot = false,
                ForceCaptchaRequired = true,
                KnownDevice = false,
                UserIsNew = false,
                UserIsVerified = false,
                UserIsOnCloud = false,
                AtFailedLoginCeiling = false,
                ExpectedResult = true
            }
        },
        // Known Users
        new [] // at failed login ceiling
        {
            new CaptchaStateTestCase
            {
                UserIsNull = false,
                IsBot = false,
                ForceCaptchaRequired = false,
                KnownDevice = true,
                UserIsNew = false,
                UserIsVerified = false,
                UserIsOnCloud = false,
                AtFailedLoginCeiling = true,
                ExpectedResult = true
            }
        },
        new [] // known device
        {
            new CaptchaStateTestCase
            {
                UserIsNull = false,
                IsBot = false,
                ForceCaptchaRequired = false,
                KnownDevice = true,
                UserIsNew = false,
                UserIsVerified = false,
                UserIsOnCloud = false,
                AtFailedLoginCeiling = false,
                ExpectedResult = false
            }
        },
        // Old Users
        new [] // is bot
        {
            new CaptchaStateTestCase
            {
                UserIsNull = false,
                IsBot = true,
                ForceCaptchaRequired = false,
                KnownDevice = false,
                UserIsNew = false,
                UserIsVerified = true,
                UserIsOnCloud = true,
                AtFailedLoginCeiling = false,
                ExpectedResult = true
            }
        },
        new [] // force captcha on
        {
            new CaptchaStateTestCase
            {
                UserIsNull = false,
                IsBot = false,
                ForceCaptchaRequired = true,
                KnownDevice = false,
                UserIsNew = false,
                UserIsVerified = true,
                UserIsOnCloud = true,
                AtFailedLoginCeiling = false,
                ExpectedResult = true
            }
        },
        // New User
        new [] // user is new, unverified, and on cloud
        {
            new CaptchaStateTestCase
            {
                UserIsNull = false,
                IsBot = false,
                ForceCaptchaRequired = false,
                KnownDevice = false,
                UserIsNew = true,
                UserIsVerified = false,
                UserIsOnCloud = true,
                AtFailedLoginCeiling = false,
                ExpectedResult = false
            }
        },
    };

    [Theory]
    [BitMemberAutoData(nameof(RequireCaptchaValidationTestCases))]
    public void RequireCaptchaValidation(CaptchaStateTestCase testCase, SutProvider<HCaptchaValidationService> sutProvider, CustomValidatorRequestContext validatorContext)
    {
        var currentContext = sutProvider.Create<ICurrentContext>();
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        globalSettings.SelfHosted = !testCase.UserIsOnCloud;
        globalSettings.Captcha.ForceCaptchaRequired = testCase.ForceCaptchaRequired;
        globalSettings.Captcha.MaximumFailedLoginAttempts = testCase.AtFailedLoginCeiling ? 1 : 2;
        currentContext.IsBot = testCase.IsBot;
        validatorContext.KnownDevice = testCase.KnownDevice;
        validatorContext.User.FailedLoginCount = 1;
        validatorContext.User.EmailVerified = testCase.UserIsVerified;
        validatorContext.User.CreationDate = testCase.UserIsNew ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(-25);
        validatorContext.User = testCase.UserIsNull ? null : validatorContext.User;

        var result = sutProvider.Sut.RequireCaptchaValidation(currentContext, validatorContext);

        Assert.Equal(testCase.ExpectedResult, result);
    }
}
