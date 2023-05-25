using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations/{orgId}/auth-requests")]
[Authorize("Application")]
public class OrganizationAuthRequestsController : Controller
{
}
