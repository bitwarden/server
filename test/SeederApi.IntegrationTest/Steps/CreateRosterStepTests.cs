using System.Text.RegularExpressions;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateRosterStepTests
{
    [Fact]
    public void NoOverride_OwnerRoleUserGetsPrefixAtDomain()
    {
        var reader = new StubSeedReader().Add("rosters.test", TwoUserRoster());
        var context = NewContext(new SeederSettings(), reader);
        PreloadOrganization(context);

        new CreateRosterStep("test").Execute(context);

        Assert.NotNull(context.Owner);
        Assert.Equal($"the.owner@{TestDomain}", context.Owner!.Email);
    }

    [Fact]
    public void WithOwnerEmailOverride_FirstOwnerRoleUserGetsOverride()
    {
        var reader = new StubSeedReader().Add("rosters.test", TwoUserRoster());
        var context = NewContext(new SeederSettings(OwnerEmailOverride: "owner-override@bw.example"), reader);
        PreloadOrganization(context);

        new CreateRosterStep("test").Execute(context);

        Assert.Equal("owner-override@bw.example", context.Owner!.Email);
    }

    [Fact]
    public void WithOwnerEmailOverride_NonOwnerUsersKeepPrefixAtDomain()
    {
        var reader = new StubSeedReader().Add("rosters.test", TwoUserRoster());
        var context = NewContext(new SeederSettings(OwnerEmailOverride: "owner-override@bw.example"), reader);
        PreloadOrganization(context);

        new CreateRosterStep("test").Execute(context);

        var nonOwner = context.Users.Single(u => u.Email != "owner-override@bw.example");
        Assert.Equal($"member.one@{TestDomain}", nonOwner.Email);
    }

    [Fact]
    public void WithOwnerEmailOverride_GroupMembershipResolvedByPrefix()
    {
        // Group "Everyone" references both users by FirstName.LastName prefix, NOT by email.
        // Override changes the stored email but not the lookup key, so membership must remain intact.
        var reader = new StubSeedReader().Add("rosters.test", RosterWithGroup());
        var context = NewContext(new SeederSettings(OwnerEmailOverride: "owner-override@bw.example"), reader);
        PreloadOrganization(context);

        new CreateRosterStep("test").Execute(context);

        Assert.Single(context.Groups);
        Assert.Equal(2, context.GroupUsers.Count);
        var orgUserIds = context.OrganizationUsers.Select(ou => ou.Id).ToHashSet();
        Assert.All(context.GroupUsers, gu => Assert.Contains(gu.OrganizationUserId, orgUserIds));
    }

    [Fact]
    public void WithOwnerEmailOverride_AndManglingEnabled_OverrideIsMangled()
    {
        var mangler = new ManglerService();
        var reader = new StubSeedReader().Add("rosters.test", TwoUserRoster());
        var context = NewContextWithMangler(
            new SeederSettings(OwnerEmailOverride: "jared@bw.example"),
            mangler,
            reader);
        PreloadOrganization(context);

        new CreateRosterStep("test").Execute(context);

        Assert.NotEqual("jared@bw.example", context.Owner!.Email);
        Assert.Matches(new Regex(@"^[a-f0-9]{8}\+jared@bw\.example$"), context.Owner.Email);

        // Non-owner roster users still get the mangled {prefix}@{domain} form, not the override.
        var nonOwner = context.Users.Single(u => u.Id != context.Owner.Id);
        Assert.Matches(new Regex($@"^[a-f0-9]{{8}}\+member\.one@{Regex.Escape(TestDomain)}$"), nonOwner.Email);
    }

    [Fact]
    public void WithOwnerEmailOverride_OnlyFirstOwnerRoleUserGetsOverride()
    {
        // If a roster declares two owner-role users, only the first one receives the override.
        // Second owner falls back to firstname.lastname@domain.
        var roster = new SeedRoster
        {
            Users =
            [
                new SeedRosterUser { FirstName = "First", LastName = "Owner", Role = "owner" },
                new SeedRosterUser { FirstName = "Second", LastName = "Owner", Role = "owner" }
            ]
        };
        var reader = new StubSeedReader().Add("rosters.test", roster);
        var context = NewContext(new SeederSettings(OwnerEmailOverride: "override@bw.example"), reader);
        PreloadOrganization(context);

        new CreateRosterStep("test").Execute(context);

        var emails = context.Users.Select(u => u.Email).OrderBy(e => e).ToList();
        Assert.Contains("override@bw.example", emails);
        Assert.Contains($"second.owner@{TestDomain}", emails);
        Assert.Equal(2, emails.Count);
    }

    private static SeedRoster TwoUserRoster() => new()
    {
        Users =
        [
            new SeedRosterUser { FirstName = "The", LastName = "Owner", Role = "owner" },
            new SeedRosterUser { FirstName = "Member", LastName = "One", Role = "user" }
        ]
    };

    private static SeedRoster RosterWithGroup() => new()
    {
        Users =
        [
            new SeedRosterUser { FirstName = "The", LastName = "Owner", Role = "owner" },
            new SeedRosterUser { FirstName = "Member", LastName = "One", Role = "user" }
        ],
        Groups =
        [
            new SeedRosterGroup { Name = "Everyone", Members = ["the.owner", "member.one"] }
        ]
    };
}
