using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerResetPasswordEnrollmentTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerResetPasswordEnrollmentTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"reset-pw-enrollment-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail,
            passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
            Data = "{}"
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PutResetPasswordEnrollment_WhenUserEnrollsSelf_ReturnsOk()
    {
        var (memberEmail, memberOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(memberEmail);

        var request = new { ResetPasswordKey = "2.enc-key", MasterPasswordHash = "master_password_hash" };

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{memberOrgUser.UserId}/reset-password-enrollment",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PutResetPasswordEnrollment_WhenUserWithdrawsSelf_ReturnsOk()
    {
        var (memberEmail, memberOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        memberOrgUser.ResetPasswordKey = "existing-reset-password-key";
        await organizationUserRepository.ReplaceAsync(memberOrgUser);

        await _loginHelper.LoginAsync(memberEmail);

        var request = new { ResetPasswordKey = (string?)null, MasterPasswordHash = (string?)null };

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{memberOrgUser.UserId}/reset-password-enrollment",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
