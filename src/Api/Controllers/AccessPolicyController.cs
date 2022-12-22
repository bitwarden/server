using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.Utilities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[SecretsManager]
public class AccessPolicyController : Controller
{
    private readonly ICreateAccessPoliciesCommand _createAccessPoliciesCommand;
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IUserService _userService;

    public AccessPolicyController(
        IUserService userService,
        IAccessPolicyRepository accessPolicyRepository,
        ICreateAccessPoliciesCommand createAccessPoliciesCommand)
    {
        _userService = userService;
        _createAccessPoliciesCommand = createAccessPoliciesCommand;
        _accessPolicyRepository = accessPolicyRepository;
    }

    [HttpPost("projects/{id}/access-policies")]
    public async Task CreateProjectAccessPoliciesAsync([FromRoute] Guid id, [FromBody] AccessPoliciesCreateRequest request)
    {
        var policies = request.ToBaseAccessPoliciesForProject(id);
        var results = await _createAccessPoliciesCommand.CreateAsync(policies);
    }
}
