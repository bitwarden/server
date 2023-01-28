using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

[SutProviderCustomize]
public class ValidateRedemptionTokenCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateRedemptionTokenAsync_CannotUnprotect_ReturnsFalse(SutProvider<ValidateRedemptionTokenCommand> sutProvider,
        string encryptedString)
    {
        sutProvider
            .GetDependency<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>()
            .TryUnprotect(encryptedString, out _)
            .Returns(call =>
            {
                call[1] = null;
                return false;
            });

        var (valid, sponsorship) = await sutProvider.Sut.ValidateRedemptionTokenAsync(encryptedString, null);
        Assert.False(valid);
        Assert.Null(sponsorship);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateRedemptionTokenAsync_NoSponsorship_ReturnsFalse(SutProvider<ValidateRedemptionTokenCommand> sutProvider,
        string encryptedString, OrganizationSponsorshipOfferTokenable tokenable)
    {
        sutProvider
            .GetDependency<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>()
            .TryUnprotect(encryptedString, out _)
            .Returns(call =>
            {
                call[1] = tokenable;
                return true;
            });

        var (valid, sponsorship) = await sutProvider.Sut.ValidateRedemptionTokenAsync(encryptedString, "test@email.com");
        Assert.False(valid);
        Assert.Null(sponsorship);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateRedemptionTokenAsync_ValidSponsorship_ReturnsFalse(SutProvider<ValidateRedemptionTokenCommand> sutProvider,
        string encryptedString, string email, OrganizationSponsorshipOfferTokenable tokenable)
    {
        tokenable.Email = email;

        sutProvider
            .GetDependency<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>()
            .TryUnprotect(encryptedString, out _)
            .Returns(call =>
            {
                call[1] = tokenable;
                return true;
            });

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetByIdAsync(tokenable.Id)
            .Returns(new OrganizationSponsorship
            {
                Id = tokenable.Id,
                PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
                OfferedToEmail = email
            });

        var (valid, sponsorship) = await sutProvider.Sut
            .ValidateRedemptionTokenAsync(encryptedString, email);

        Assert.True(valid);
        Assert.NotNull(sponsorship);
    }
}
