using System.ComponentModel.DataAnnotations;
using AutoFixture;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Auth.AutoFixture;

internal class RegisterFinishRequestModelCustomization : ICustomization
{
    [StrictEmailAddress, StringLength(256)]
    public required string Email { get; set; }
    public required KdfType Kdf { get; set; }
    public required int KdfIterations { get; set; }
    public string? EmailVerificationToken { get; set; }
    public string? OrgInviteToken { get; set; }
    public string? OrgSponsoredFreeFamilyPlanToken { get; set; }
    public string? AcceptEmergencyAccessInviteToken { get; set; }
    public string? ProviderInviteToken { get; set; }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<RegisterFinishRequestModel>(composer => composer
            .With(o => o.Email, Email)
            .With(o => o.Kdf, Kdf)
            .With(o => o.KdfIterations, KdfIterations)
            .With(o => o.EmailVerificationToken, EmailVerificationToken)
            .With(o => o.OrgInviteToken, OrgInviteToken)
            .With(o => o.OrgSponsoredFreeFamilyPlanToken, OrgSponsoredFreeFamilyPlanToken)
            .With(o => o.AcceptEmergencyAccessInviteToken, AcceptEmergencyAccessInviteToken)
            .With(o => o.ProviderInviteToken, ProviderInviteToken));
    }
}

public class RegisterFinishRequestModelCustomizeAttribute : BitCustomizeAttribute
{
    public string _email { get; set; } = "{0}@email.com";
    public KdfType _kdf { get; set; } = KdfType.PBKDF2_SHA256;
    public int _kdfIterations { get; set; } = AuthConstants.PBKDF2_ITERATIONS.Default;
    public string? _emailVerificationToken { get; set; }
    public string? _orgInviteToken { get; set; }
    public string? _orgSponsoredFreeFamilyPlanToken { get; set; }
    public string? _acceptEmergencyAccessInviteToken { get; set; }
    public string? _providerInviteToken { get; set; }

    public override ICustomization GetCustomization() => new RegisterFinishRequestModelCustomization()
    {
        Email = _email,
        Kdf = _kdf,
        KdfIterations = _kdfIterations,
        EmailVerificationToken = _emailVerificationToken,
        OrgInviteToken = _orgInviteToken,
        OrgSponsoredFreeFamilyPlanToken = _orgSponsoredFreeFamilyPlanToken,
        AcceptEmergencyAccessInviteToken = _acceptEmergencyAccessInviteToken,
        ProviderInviteToken = _providerInviteToken
    };
}
