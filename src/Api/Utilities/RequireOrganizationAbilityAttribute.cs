using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace Bit.Core.Utilities;

// <summary>
/// Specifies that the class or method that this attribute is applied to requires the specified organization ability
/// to be enabled. If the organization ability is not enabled, a <see cref="FeatureUnavailableException"/> is thrown
// </summary>
public class RequireOrganizationAbilityAttribute : ActionFilterAttribute
{
  private readonly string _abilityKey;

  /// <summary>
  /// Initializes a new instance of the <see cref="RequireOrganizationAbilityAttribute"/> class with the specified ability key.
  /// </summary>
  /// <param name="abilityKey">The name of the organization ability to require. Should be a valid boolean property on the <see cref="OrganizationAbility"/> class.</param>
  // </summary>
  public RequireOrganizationAbilityAttribute(string abilityKey)
  {
    _abilityKey = abilityKey;
  }

  public async override void OnActionExecuting(ActionExecutingContext context)
  {
    var orgId = context.HttpContext.GetOrganizationId();
    if (orgId == Guid.Empty)
    {
      throw new Exception("Route parameter 'orgId' or 'organizationId' is missing or invalid.");
    }

    var orgAbilityCacheService = context.HttpContext.RequestServices.GetRequiredService<IOrganizationAbilityCacheService>();

    var orgAbility = await orgAbilityCacheService.GetOrganizationAbilityAsync(orgId);
    if (orgAbility == null)
    {
      throw new BadRequestException("The user's organization does not have access to this feature in their plan.");
    }

    PropertyInfo? ability = orgAbility.GetType().GetProperty(_abilityKey);
    if (ability == null || ability.PropertyType != typeof(bool))
    {
      throw new Exception($"Ability '{_abilityKey}' is not a valid Organization ability. See the OrganizationAbility class for valid abilities.");
    }

    var hasAbility = (bool)ability.GetValue(orgAbility)!;
    if (!hasAbility)
    {
      throw new BadRequestException("The user's organization does not have access to this feature in their plan.");
    }
  }
}
