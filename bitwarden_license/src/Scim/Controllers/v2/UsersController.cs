using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2
{
    [Route("v2/{organizationId}/users")]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ScimSettings _scimSettings;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService,
            IUserRepository userRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOptions<ScimSettings> billingSettings,
            ILogger<UsersController> logger)
        {
            _userService = userService;
            _userRepository = userRepository;
            _organizationUserRepository = organizationUserRepository;
            _scimSettings = billingSettings?.Value;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid organizationId, Guid id)
        {
            var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(id);
            if (orgUser == null || orgUser.OrganizationId != organizationId)
            {
                return new ObjectResult(new ScimErrorResponseModel { Status = 404, Detail = "User not found." });
            }
            return new ObjectResult(new ScimUserResponseModel(orgUser));
        }

        [HttpGet("")]
        public async Task<IActionResult> Get(
            Guid organizationId,
            [FromQuery] string filter,
            [FromQuery] int? count,
            [FromQuery] int? startIndex)
        {
            string emailFilter = null;
            string usernameFilter = null;
            string externalIdFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filter.StartsWith("userName eq "))
                {
                    usernameFilter = filter.Substring(12).Trim('"');
                    if (usernameFilter.Contains("@"))
                    {
                        emailFilter = usernameFilter;
                    }
                }
                else if (filter.StartsWith("externalId eq "))
                {
                    externalIdFilter = filter.Substring(14).Trim('"');
                }
            }

            var userList = new List<ScimUserResponseModel> { };
            var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
            var totalResults = orgUsers.Count;
            if (!string.IsNullOrWhiteSpace(emailFilter))
            {
                var orgUser = orgUsers.FirstOrDefault(ou => ou.Email == emailFilter);
                if (orgUser != null)
                {
                    userList.Add(new ScimUserResponseModel(orgUser));
                }
                totalResults = userList.Count;
            }
            else if (!string.IsNullOrWhiteSpace(externalIdFilter))
            {
                var orgUser = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalIdFilter);
                if (orgUser != null)
                {
                    userList.Add(new ScimUserResponseModel(orgUser));
                }
                totalResults = userList.Count;
            }
            else if (string.IsNullOrWhiteSpace(filter) && startIndex.HasValue && count.HasValue)
            {
                userList = orgUsers.OrderBy(ou => ou.Email)
                    .Skip(startIndex.Value - 1) // Should this be offset by 1 or not?
                    .Take(count.Value)
                    .Select(ou => new ScimUserResponseModel(ou))
                    .ToList();
            }

            var result = new ScimListResponseModel<ScimUserResponseModel>
            {
                Resources = userList,
                ItemsPerPage = count.GetValueOrDefault(userList.Count),
                TotalResults = totalResults,
                StartIndex = startIndex.GetValueOrDefault(1),
            };
            return new ObjectResult(result);
        }

        [HttpPost("")]
        public async Task<IActionResult> PostCreate(Guid organizationId, [FromBody] object model)
        {
            return new CreatedResult("", new { });
        }
    }
}
