using System.Collections.Specialized;
using System.Security.Claims;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

public class CurrentContextBackfillServiceTests
{
    private readonly CurrentContextBackfillService _sut = new(NullLogger<CurrentContextBackfillService>.Instance);

    // === Subject path edges ===

    [Fact]
    public void Apply_SubjectWithMalformedSubClaim_LeavesUserIdNull()
    {
        // Defensive Guid.TryParse must not throw on a malformed `sub`.
        var currentContext = MakeCurrentContext();
        var subject = MakeSubject(sub: "not-a-guid", device: "device-id");

        _sut.Apply(currentContext, MakeRequest(), subject: subject);

        Assert.Null(currentContext.UserId);
        Assert.Equal("device-id", currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_SubjectMissingSubClaim_LeavesUserIdNull()
    {
        var currentContext = MakeCurrentContext();
        var subject = MakeSubject(device: "device-id");

        _sut.Apply(currentContext, MakeRequest(), subject: subject);

        Assert.Null(currentContext.UserId);
        Assert.Equal("device-id", currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_SubjectWithSubButNoDeviceClaim_PopulatesUserIdOnly()
    {
        var userId = Guid.NewGuid();
        var currentContext = MakeCurrentContext();
        var subject = MakeSubject(sub: userId.ToString());

        _sut.Apply(currentContext, MakeRequest(), subject: subject);

        Assert.Equal(userId, currentContext.UserId);
        Assert.Null(currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_SubjectWithDeviceButNoSubClaim_PopulatesDeviceIdentifierOnly()
    {
        var currentContext = MakeCurrentContext();
        var subject = MakeSubject(device: "device-id");

        _sut.Apply(currentContext, MakeRequest(), subject: subject);

        Assert.Null(currentContext.UserId);
        Assert.Equal("device-id", currentContext.DeviceIdentifier);
    }

    // === ValidatorContext path edges ===

    [Fact]
    public void Apply_ValidatorContextWithDeviceButNoUser_PopulatesDeviceIdentifierOnly()
    {
        var currentContext = MakeCurrentContext();
        var validatorContext = MakeValidatorContext(deviceIdentifier: "device-id");

        _sut.Apply(currentContext, MakeRequest(), validatorContext: validatorContext);

        Assert.Null(currentContext.UserId);
        Assert.Equal("device-id", currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_ValidatorContextUserIdIsGuidEmpty_PopulatesAsGuidEmpty()
    {
        // Pin current behavior — Guid.Empty satisfies the `is Guid` pattern and gets
        // assigned. If you want to reject Guid.Empty as invalid, this test will tell
        // you the helper changed.
        var currentContext = MakeCurrentContext();
        var validatorContext = MakeValidatorContext(userId: Guid.Empty);

        _sut.Apply(currentContext, MakeRequest(), validatorContext: validatorContext);

        Assert.Equal(Guid.Empty, currentContext.UserId);
    }

    // === Source precedence ===

    [Fact]
    public void Apply_SubjectAndValidatorContextBothHaveUserId_SubjectWins()
    {
        var subjectUserId = Guid.NewGuid();
        var validatorUserId = Guid.NewGuid();
        var currentContext = MakeCurrentContext();
        var subject = MakeSubject(sub: subjectUserId.ToString());
        var validatorContext = MakeValidatorContext(userId: validatorUserId);

        _sut.Apply(currentContext, MakeRequest(), subject: subject, validatorContext: validatorContext);

        Assert.Equal(subjectUserId, currentContext.UserId);
    }

    [Fact]
    public void Apply_SubjectAndValidatorContextBothHaveDevice_SubjectWins()
    {
        var currentContext = MakeCurrentContext();
        var subject = MakeSubject(device: "subject-device-id");
        var validatorContext = MakeValidatorContext(deviceIdentifier: "validator-device-id");

        _sut.Apply(currentContext, MakeRequest(), subject: subject, validatorContext: validatorContext);

        Assert.Equal("subject-device-id", currentContext.DeviceIdentifier);
    }

    // === Empty/whitespace normalization ===

    [Fact]
    public void Apply_FormBodyDeviceIdentifierIsEmpty_TreatedAsNull()
    {
        // Without normalization, "" would assign to DeviceIdentifier and block any
        // subsequent back-fill attempt because ??= only fires on null.
        var currentContext = MakeCurrentContext();
        var request = MakeRequest(formBodyDeviceIdentifier: "");

        _sut.Apply(currentContext, request);

        Assert.Null(currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_FormBodyDeviceIdentifierIsWhitespace_TreatedAsNull()
    {
        var currentContext = MakeCurrentContext();
        var request = MakeRequest(formBodyDeviceIdentifier: "   ");

        _sut.Apply(currentContext, request);

        Assert.Null(currentContext.DeviceIdentifier);
    }

    // === Mixed middleware-populated state ===

    [Fact]
    public void Apply_CurrentContextUserIdAlreadySet_OnlyDeviceIdentifierBackfills()
    {
        var existingUserId = Guid.NewGuid();
        var currentContext = MakeCurrentContext(userId: existingUserId);
        var validatorContext = MakeValidatorContext(userId: Guid.NewGuid(), deviceIdentifier: "validator-device");

        _sut.Apply(currentContext, MakeRequest(), validatorContext: validatorContext);

        Assert.Equal(existingUserId, currentContext.UserId);
        Assert.Equal("validator-device", currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_CurrentContextDeviceIdentifierAlreadySet_OnlyUserIdBackfills()
    {
        const string existingDeviceId = "middleware-device-id";
        var currentContext = MakeCurrentContext(deviceIdentifier: existingDeviceId);
        var validatorUserId = Guid.NewGuid();
        var validatorContext = MakeValidatorContext(userId: validatorUserId, deviceIdentifier: "validator-device");

        _sut.Apply(currentContext, MakeRequest(), validatorContext: validatorContext);

        Assert.Equal(validatorUserId, currentContext.UserId);
        Assert.Equal(existingDeviceId, currentContext.DeviceIdentifier);
    }

    // === Defensive null-handling ===

    [Fact]
    public void Apply_AllSourcesEmpty_LeavesCurrentContextUntouched()
    {
        var currentContext = MakeCurrentContext();

        _sut.Apply(currentContext, MakeRequest());

        Assert.Null(currentContext.UserId);
        Assert.Null(currentContext.DeviceIdentifier);
    }

    [Fact]
    public void Apply_NullRequest_DoesNotThrow()
    {
        // The helper guards request with ?.Raw so this should be a safe no-op.
        var currentContext = MakeCurrentContext();

        _sut.Apply(currentContext, request: null);

        Assert.Null(currentContext.UserId);
        Assert.Null(currentContext.DeviceIdentifier);
    }

    // === Best-effort failure handling ===

    [Fact]
    public void Apply_WhenCurrentContextSetterThrows_SwallowsAndLogsWarning()
    {
        // Critical contract: a broken back-fill must NEVER block a token refresh or
        // login. If any source/setter throws, the service catches it, logs a warning,
        // and returns silently so the downstream grant validation proceeds.
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = null;
        currentContext.When(x => x.UserId = Arg.Any<Guid?>())
            .Do(_ => throw new InvalidOperationException("simulated failure"));

        var logger = new FakeLogger<CurrentContextBackfillService>();
        var sut = new CurrentContextBackfillService(logger);
        var subject = MakeSubject(sub: Guid.NewGuid().ToString());

        var exception = Record.Exception(() =>
            sut.Apply(currentContext, MakeRequest(), subject: subject));

        Assert.Null(exception);
        Assert.Contains(logger.Collector.GetSnapshot(),
            l => l.Level == LogLevel.Warning);
    }

    // === Test helpers ===

    private static ICurrentContext MakeCurrentContext(Guid? userId = null, string deviceIdentifier = null)
    {
        var ctx = Substitute.For<ICurrentContext>();
        ctx.UserId = userId;
        // Explicitly null — NSubstitute auto-substitutes string properties to string.Empty
        // otherwise, which would break the ??= semantics we're testing.
        ctx.DeviceIdentifier = deviceIdentifier;
        return ctx;
    }

    private static ClaimsPrincipal MakeSubject(string sub = null, string device = null)
    {
        var claims = new List<Claim>();
        if (sub is not null)
        {
            claims.Add(new Claim(JwtClaimTypes.Subject, sub));
        }
        if (device is not null)
        {
            claims.Add(new Claim(Claims.Device, device));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static CustomValidatorRequestContext MakeValidatorContext(
        Guid? userId = null,
        string deviceIdentifier = null)
    {
        return new CustomValidatorRequestContext
        {
            User = userId is null ? null : new User { Id = userId.Value },
            Device = deviceIdentifier is null ? null : new Device { Identifier = deviceIdentifier },
        };
    }

    private static ValidatedRequest MakeRequest(string formBodyDeviceIdentifier = null)
    {
        var raw = new NameValueCollection();
        if (formBodyDeviceIdentifier is not null)
        {
            raw["DeviceIdentifier"] = formBodyDeviceIdentifier;
        }
        return new ValidatedRequest { Raw = raw };
    }
}
