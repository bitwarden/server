using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business.Tokenables;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.Business.Tokenables;

public class OrganizationSponsorshipOfferTokenableTests
{
    public static IEnumerable<object[]> PlanSponsorshipTypes() => Enum.GetValues<PlanSponsorshipType>().Select(x => new object[] { x });

    [Fact]
    public void IsInvalidIfIdentifierIsWrong()
    {
        var token = new OrganizationSponsorshipOfferTokenable()
        {
            Email = "email",
            Id = Guid.NewGuid(),
            Identifier = "not correct",
            SponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
        };

        Assert.False(token.Valid);
    }

    [Fact]
    public void IsInvalidIfIdIsDefault()
    {
        var token = new OrganizationSponsorshipOfferTokenable()
        {
            Email = "email",
            Id = default,
            SponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
        };

        Assert.False(token.Valid);
    }


    [Fact]
    public void IsInvalidIfEmailIsEmpty()
    {
        var token = new OrganizationSponsorshipOfferTokenable()
        {
            Email = "",
            Id = Guid.NewGuid(),
            SponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
        };

        Assert.False(token.Valid);
    }

    [Theory, BitAutoData]
    public void IsValid_Success(OrganizationSponsorship sponsorship)
    {
        var token = new OrganizationSponsorshipOfferTokenable(sponsorship);

        Assert.True(token.IsValid(sponsorship, sponsorship.OfferedToEmail));
    }

    [Theory, BitAutoData]
    public void IsValid_RequiresNonNullSponsorship(OrganizationSponsorship sponsorship)
    {
        var token = new OrganizationSponsorshipOfferTokenable(sponsorship);

        Assert.False(token.IsValid(null, sponsorship.OfferedToEmail));
    }

    [Theory, BitAutoData]
    public void IsValid_RequiresCurrentEmailToBeSameAsOfferedToEmail(OrganizationSponsorship sponsorship, string currentEmail)
    {
        var token = new OrganizationSponsorshipOfferTokenable(sponsorship);

        Assert.False(token.IsValid(sponsorship, currentEmail));
    }

    [Theory, BitAutoData]
    public void IsValid_RequiresSameSponsorshipId(OrganizationSponsorship sponsorship1, OrganizationSponsorship sponsorship2)
    {
        sponsorship1.Id = sponsorship2.Id;

        var token = new OrganizationSponsorshipOfferTokenable(sponsorship1);

        Assert.False(token.IsValid(sponsorship2, sponsorship1.OfferedToEmail));
    }

    [Theory, BitAutoData]
    public void IsValid_RequiresSameEmail(OrganizationSponsorship sponsorship1, OrganizationSponsorship sponsorship2)
    {
        sponsorship1.OfferedToEmail = sponsorship2.OfferedToEmail;

        var token = new OrganizationSponsorshipOfferTokenable(sponsorship1);

        Assert.False(token.IsValid(sponsorship2, sponsorship1.OfferedToEmail));
    }

    [Theory, BitAutoData]
    public void Constructor_GrabsIdFromSponsorship(OrganizationSponsorship sponsorship)
    {
        var token = new OrganizationSponsorshipOfferTokenable(sponsorship);

        Assert.Equal(sponsorship.Id, token.Id);
    }

    [Theory, BitAutoData]
    public void Constructor_GrabsEmailFromSponsorshipOfferedToEmail(OrganizationSponsorship sponsorship)
    {
        var token = new OrganizationSponsorshipOfferTokenable(sponsorship);

        Assert.Equal(sponsorship.OfferedToEmail, token.Email);
    }

    [Theory, BitMemberAutoData(nameof(PlanSponsorshipTypes))]
    public void Constructor_GrabsSponsorshipType(PlanSponsorshipType planSponsorshipType,
        OrganizationSponsorship sponsorship)
    {
        sponsorship.PlanSponsorshipType = planSponsorshipType;
        var token = new OrganizationSponsorshipOfferTokenable(sponsorship);

        Assert.Equal(sponsorship.PlanSponsorshipType, token.SponsorshipType);
    }

    [Theory, BitAutoData]
    public void Constructor_DefaultId_Throws(OrganizationSponsorship sponsorship)
    {
        sponsorship.Id = default;

        Assert.Throws<ArgumentException>(() => new OrganizationSponsorshipOfferTokenable(sponsorship));
    }

    [Theory, BitAutoData]
    public void Constructor_NoOfferedToEmail_Throws(OrganizationSponsorship sponsorship)
    {
        sponsorship.OfferedToEmail = null;

        Assert.Throws<ArgumentException>(() => new OrganizationSponsorshipOfferTokenable(sponsorship));
    }

    [Theory, BitAutoData]
    public void Constructor_EmptyOfferedToEmail_Throws(OrganizationSponsorship sponsorship)
    {
        sponsorship.OfferedToEmail = "";

        Assert.Throws<ArgumentException>(() => new OrganizationSponsorshipOfferTokenable(sponsorship));
    }

    [Theory, BitAutoData]
    public void Constructor_NoPlanSponsorshipType_Throws(OrganizationSponsorship sponsorship)
    {
        sponsorship.PlanSponsorshipType = null;

        Assert.Throws<ArgumentException>(() => new OrganizationSponsorshipOfferTokenable(sponsorship));
    }
}
