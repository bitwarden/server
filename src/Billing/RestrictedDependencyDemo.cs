// Demo only. Each section compiles under `dotnet build src/Billing --no-dependencies -p:RestrictedDemo=<section>`
// and shows what an engineer sees when they add a use of the restricted IUserService.
#if RESTRICTED_DEMO_ACCESS || RESTRICTED_DEMO_MEMBER || RESTRICTED_DEMO_EXCEPTIONS || RESTRICTED_DEMO_EXPIRED || RESTRICTED_DEMO_INVALID
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.RestrictedDependencies;

namespace Bit.Billing.RestrictedDependencyDemo;

#if RESTRICTED_DEMO_ACCESS
public class NewInjection
{
    private readonly IUserService _userService;

    public NewInjection(IUserService userService)
    {
        _userService = userService;
    }

    public bool HasService => _userService is not null;
}

public class LocatorResolution
{
    private readonly IServiceProvider _provider;

    public LocatorResolution(IServiceProvider provider)
    {
        _provider = provider;
    }

    public bool HasService => _provider.GetRequiredService<IUserService>() is not null;
}

public class ConcreteReference
{
    public Type Implementation => typeof(UserService);
}

public class EscapeThroughProperty
{
    public IUserService? Service { get; set; }
}
#endif

#if RESTRICTED_DEMO_MEMBER
[RestrictedDependencyException(typeof(IUserService), Owner = "team-billing", Reason = "PM-43148 demo", Expires = "2027-01-01")]
public class MemberBeyondBudget
{
    private readonly IUserService _userService;

    public MemberBeyondBudget(IUserService userService)
    {
        _userService = userService;
    }

    public Task<bool> CanAccessPremiumAsync(User user) => _userService.CanAccessPremium(user);
}

[RestrictedDependencyException(typeof(IUserService), Owner = "team-billing", Reason = "PM-43148 demo", Expires = "2027-01-01")]
public class ForbiddenMember
{
    private readonly IUserService _userService;

    public ForbiddenMember(IUserService userService)
    {
        _userService = userService;
    }

    public string UserName(ClaimsPrincipal principal) => _userService.GetUserName(principal);
}
#endif

#if RESTRICTED_DEMO_EXCEPTIONS
[RestrictedDependencyException(typeof(IUserService), Owner = "team-billing")]
public class IncompleteException
{
    private readonly IUserService _userService;

    public IncompleteException(IUserService userService)
    {
        _userService = userService;
    }

    public bool HasService => _userService is not null;
}

[SuppressMessage("RestrictedDependencies", "BW0005", Justification = "Demo: this is exactly what the analyzer refuses to honor.")]
public class AttributeSuppression
{
    private readonly IUserService _userService;

    public AttributeSuppression(IUserService userService)
    {
        _userService = userService;
    }

    public bool HasService => _userService is not null;
}
#endif

#if RESTRICTED_DEMO_EXPIRED
[RestrictedDependencyException(typeof(IUserService), Owner = "team-billing", Reason = "PM-43148 demo", Expires = "2025-01-01")]
public class ExpiredException
{
    private readonly IUserService _userService;

    public ExpiredException(IUserService userService)
    {
        _userService = userService;
    }

    public bool HasService => _userService is not null;
}
#endif

#if RESTRICTED_DEMO_INVALID
[RestrictedDependency]
public interface IDemoRestricted
{
    void Nothing();
}

public class UnmarkedOwner
{
    [RestrictedDependency]
    public void Marked()
    {
    }
}
#endif
#endif
