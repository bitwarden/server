using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Bit.Api.Controllers;

public class TestOdata
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[Route("report")]
[Authorize("Application")]
public class ReportController : ODataController
{
    private readonly ICollectionService _collectionService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;

    public ReportController(ICollectionService collectionService, IOrganizationUserRepository organizationUserRepository, IUserService userService)
    {
        _collectionService = collectionService;
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
    }

    [HttpGet("UserOrganizations")]
    [EnableQuery]
    public async Task<IEnumerable<ProfileOrganizationResponseModel>> GetOrganizations()
    {
        var userId = _userService.GetProperUserId(User);
        var organizationUserDetails = await _organizationUserRepository.GetManyDetailsByUserAsync(userId.Value,
            OrganizationUserStatusType.Confirmed);
        var responseData = organizationUserDetails.Select(o => new ProfileOrganizationResponseModel(o));
        return responseData;
    }

    [HttpGet("Members({key})")]
    [EnableQuery]
    public async Task<IEnumerable<MemberResponseModel>> GetOrganizationMembers([FromODataUri] Guid key)
    {
        var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(key);
        var memberResponsesTasks = users.Select(async u => new MemberResponseModel(u,
                await _userService.TwoFactorIsEnabledAsync(u), null, false));
        var memberResponses = await Task.WhenAll(memberResponsesTasks);
        return memberResponses;
    }

    // [HttpGet("")]
    // [EnableQuery]
    // public IEnumerable<TestOdata> Get()
    // {
    //   return new List<TestOdata> 
    //   { 
    //     new TestOdata { Id = 1, Name = "TestOne" },
    //     new TestOdata { Id = 2, Name = "TestTwo" },
    //     new TestOdata { Id = 3, Name = "TestThree" }
    //   };
    // }
}
