using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Repositories;
using System.Threading.Tasks;
using Bit.Admin.Models;
using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Admin.Controllers
{
    [Authorize]
    public class OrganizationsController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;

        public OrganizationsController(IOrganizationRepository organizationRepository)
        {
            _organizationRepository = organizationRepository;
        }

        public async Task<IActionResult> Index(string name = null, string userEmail = null, bool? paid = null,
            int page = 1, int count = 25)
        {
            if(page < 1)
            {
                page = 1;
            }

            if(count < 1)
            {
                count = 1;
            }

            var skip = (page - 1) * count;
            var organizations = await _organizationRepository.SearchAsync(name, userEmail, paid, skip, count);
            return View(new OrganizationsModel
            {
                Items = organizations as List<Organization>,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
                Paid = paid,
                Page = page,
                Count = count
            });
        }
    }
}
