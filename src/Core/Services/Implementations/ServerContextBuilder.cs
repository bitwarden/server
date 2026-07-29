using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Utilities;
using Bitwarden.Server.Sdk.Features;
using LaunchDarkly.Sdk;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Services.Implementations;

public class ServerContextBuilder : IContextBuilder
{
    private const string _anonymousUser = "25a15cac-58cf-4ac0-ad0f-b17c4bd92294";

    private const string _contextKindDevice = "device";
    private const string _contextKindOrganization = "organization";
    private const string _contextKindServiceAccount = "service-account";

    private const string _contextAttributeClientVersion = "client-version";
    private const string _contextAttributeClientVersionIsPrerelease = "client-version-is-prerelease";
    private const string _contextAttributeDeviceType = "device-type";
    private const string _contextAttributeClientType = "client-type";
    private const string _contextAttributeOrganizations = "organizations";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerContextBuilder(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public LaunchDarkly.Sdk.Context Build()
    {
        var currentContext = _httpContextAccessor.HttpContext
            ?.RequestServices.GetRequiredService<ICurrentContext>();

        if (currentContext is null)
        {
            return LaunchDarkly.Sdk.Context.Builder(_anonymousUser)
                .Kind(ContextKind.Default)
                .Anonymous(true)
                .Build();
        }

        var builder = LaunchDarkly.Sdk.Context.MultiBuilder();

        if (!string.IsNullOrWhiteSpace(currentContext.DeviceIdentifier))
        {
            var ldDevice = LaunchDarkly.Sdk.Context.Builder(currentContext.DeviceIdentifier);

            ldDevice.Kind(_contextKindDevice);
            SetCommonContextAttributes(ldDevice);

            builder.Add(ldDevice.Build());
        }

        switch (currentContext.IdentityClientType)
        {
            case IdentityClientType.User:
                {
                    ContextBuilder ldUser;
                    if (currentContext.UserId.HasValue)
                    {
                        ldUser = LaunchDarkly.Sdk.Context.Builder(currentContext.UserId.Value.ToString());
                    }
                    else
                    {
                        // group all unauthenticated activity under one anonymous user key and mark as such
                        ldUser = LaunchDarkly.Sdk.Context.Builder(_anonymousUser);
                        ldUser.Anonymous(true);
                    }

                    ldUser.Kind(ContextKind.Default);
                    SetCommonContextAttributes(ldUser);

                    if (currentContext.Organizations?.Any() ?? false)
                    {
                        var ldOrgs = currentContext.Organizations.Select(o => LdValue.Of(o.Id.ToString()));
                        ldUser.Set(_contextAttributeOrganizations, LdValue.ArrayFrom(ldOrgs));
                    }

                    builder.Add(ldUser.Build());
                }
                break;

            case IdentityClientType.Organization:
                {
                    if (currentContext.OrganizationId.HasValue)
                    {
                        var ldOrg = LaunchDarkly.Sdk.Context.Builder(currentContext.OrganizationId.Value.ToString());

                        ldOrg.Kind(_contextKindOrganization);
                        SetCommonContextAttributes(ldOrg);

                        builder.Add(ldOrg.Build());
                    }
                }
                break;

            case IdentityClientType.ServiceAccount:
                {
                    if (currentContext.UserId.HasValue)
                    {
                        var ldServiceAccount = LaunchDarkly.Sdk.Context.Builder(currentContext.UserId.Value.ToString());

                        ldServiceAccount.Kind(_contextKindServiceAccount);
                        SetCommonContextAttributes(ldServiceAccount);

                        builder.Add(ldServiceAccount.Build());
                    }
                    else if (currentContext.OrganizationId.HasValue)
                    {
                        var ldServiceAccount = LaunchDarkly.Sdk.Context.Builder(currentContext.OrganizationId.Value.ToString());

                        ldServiceAccount.Kind(_contextKindServiceAccount);
                        SetCommonContextAttributes(ldServiceAccount);

                        builder.Add(ldServiceAccount.Build());
                    }
                }
                break;

            case IdentityClientType.Send:
                {
                    var ldSend = LaunchDarkly.Sdk.Context.Builder(_anonymousUser);
                    ldSend.Anonymous(true);
                    ldSend.Kind(ContextKind.Default);
                    SetCommonContextAttributes(ldSend);

                    builder.Add(ldSend.Build());
                }
                break;
        }

        return builder.Build();

        void SetCommonContextAttributes(ContextBuilder builder)
        {
            if (currentContext.ClientVersion != null)
            {
                builder.Set(_contextAttributeClientVersion, currentContext.ClientVersion.ToString());
                builder.Set(_contextAttributeClientVersionIsPrerelease, currentContext.ClientVersionIsPrerelease);
            }

            if (currentContext.DeviceType.HasValue)
            {
                builder.Set(_contextAttributeDeviceType, (int)currentContext.DeviceType.Value);
                builder.Set(_contextAttributeClientType, (int)DeviceTypes.ToClientType(currentContext.DeviceType.Value));
            }
        }
    }
}
