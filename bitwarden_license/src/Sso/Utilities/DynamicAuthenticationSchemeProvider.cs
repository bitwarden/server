using System.Security.Cryptography.X509Certificates;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Sso.Models;
using Bit.Sso.Utilities;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Saml2P;

namespace Bit.Core.Business.Sso;

public class DynamicAuthenticationSchemeProvider : AuthenticationSchemeProvider
{
    private readonly IPostConfigureOptions<OpenIdConnectOptions> _oidcPostConfigureOptions;
    private readonly IExtendedOptionsMonitorCache<OpenIdConnectOptions> _extendedOidcOptionsMonitorCache;
    private readonly IPostConfigureOptions<Saml2Options> _saml2PostConfigureOptions;
    private readonly IExtendedOptionsMonitorCache<Saml2Options> _extendedSaml2OptionsMonitorCache;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ILogger _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly SamlEnvironment _samlEnvironment;
    private readonly TimeSpan _schemeCacheLifetime;
    private readonly Dictionary<string, DynamicAuthenticationScheme> _cachedSchemes;
    private readonly Dictionary<string, DynamicAuthenticationScheme> _cachedHandlerSchemes;
    private readonly SemaphoreSlim _semaphore;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private DateTime? _lastSchemeLoad;
    private IEnumerable<DynamicAuthenticationScheme> _schemesCopy = Array.Empty<DynamicAuthenticationScheme>();
    private IEnumerable<DynamicAuthenticationScheme> _handlerSchemesCopy = Array.Empty<DynamicAuthenticationScheme>();

    public DynamicAuthenticationSchemeProvider(
        IOptions<AuthenticationOptions> options,
        IPostConfigureOptions<OpenIdConnectOptions> oidcPostConfigureOptions,
        IOptionsMonitorCache<OpenIdConnectOptions> oidcOptionsMonitorCache,
        IPostConfigureOptions<Saml2Options> saml2PostConfigureOptions,
        IOptionsMonitorCache<Saml2Options> saml2OptionsMonitorCache,
        ISsoConfigRepository ssoConfigRepository,
        ILogger<DynamicAuthenticationSchemeProvider> logger,
        GlobalSettings globalSettings,
        SamlEnvironment samlEnvironment,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _oidcPostConfigureOptions = oidcPostConfigureOptions;
        _extendedOidcOptionsMonitorCache = oidcOptionsMonitorCache as
            IExtendedOptionsMonitorCache<OpenIdConnectOptions>;
        if (_extendedOidcOptionsMonitorCache == null)
        {
            throw new ArgumentNullException("_extendedOidcOptionsMonitorCache could not be resolved.");
        }

        _saml2PostConfigureOptions = saml2PostConfigureOptions;
        _extendedSaml2OptionsMonitorCache = saml2OptionsMonitorCache as
            IExtendedOptionsMonitorCache<Saml2Options>;
        if (_extendedSaml2OptionsMonitorCache == null)
        {
            throw new ArgumentNullException("_extendedSaml2OptionsMonitorCache could not be resolved.");
        }

        _ssoConfigRepository = ssoConfigRepository;
        _logger = logger;
        _globalSettings = globalSettings;
        _schemeCacheLifetime = TimeSpan.FromSeconds(_globalSettings.Sso?.CacheLifetimeInSeconds ?? 30);
        _samlEnvironment = samlEnvironment;
        _cachedSchemes = new Dictionary<string, DynamicAuthenticationScheme>();
        _cachedHandlerSchemes = new Dictionary<string, DynamicAuthenticationScheme>();
        _semaphore = new SemaphoreSlim(1);
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private bool CacheIsValid
    {
        get => _lastSchemeLoad.HasValue
            && _lastSchemeLoad.Value.Add(_schemeCacheLifetime) >= DateTime.UtcNow;
    }

    public override async Task<AuthenticationScheme> GetSchemeAsync(string name)
    {
        var scheme = await base.GetSchemeAsync(name);
        if (scheme != null)
        {
            return scheme;
        }

        try
        {
            var dynamicScheme = await GetDynamicSchemeAsync(name);
            return dynamicScheme;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load a dynamic authentication scheme for '{0}'", name);
        }

        return null;
    }

    public override async Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
    {
        var existingSchemes = await base.GetAllSchemesAsync();
        var schemes = new List<AuthenticationScheme>();
        schemes.AddRange(existingSchemes);

        await LoadAllDynamicSchemesIntoCacheAsync();
        schemes.AddRange(_schemesCopy);

        return schemes.ToArray();
    }

    public override async Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync()
    {
        var existingSchemes = await base.GetRequestHandlerSchemesAsync();
        var schemes = new List<AuthenticationScheme>();
        schemes.AddRange(existingSchemes);

        await LoadAllDynamicSchemesIntoCacheAsync();
        schemes.AddRange(_handlerSchemesCopy);

        return schemes.ToArray();
    }

    private async Task LoadAllDynamicSchemesIntoCacheAsync()
    {
        if (CacheIsValid)
        {
            // Our cache hasn't expired or been invalidated, ignore request
            return;
        }
        await _semaphore.WaitAsync();
        try
        {
            if (CacheIsValid)
            {
                // Just in case (double-checked locking pattern)
                return;
            }

            // Save time just in case the following operation takes longer
            var now = DateTime.UtcNow;
            var newSchemes = await _ssoConfigRepository.GetManyByRevisionNotBeforeDate(_lastSchemeLoad);

            foreach (var config in newSchemes)
            {
                DynamicAuthenticationScheme scheme;
                try
                {
                    scheme = GetSchemeFromSsoConfig(config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting configuration to scheme for '{0}'", config.Id);
                    continue;
                }
                if (scheme == null)
                {
                    continue;
                }
                SetSchemeInCache(scheme);
            }

            if (newSchemes.Any())
            {
                // Maintain "safe" copy for use in enumeration routines
                _schemesCopy = _cachedSchemes.Values.ToArray();
                _handlerSchemesCopy = _cachedHandlerSchemes.Values.ToArray();
            }
            _lastSchemeLoad = now;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private DynamicAuthenticationScheme SetSchemeInCache(DynamicAuthenticationScheme scheme)
    {
        if (!PostConfigureDynamicScheme(scheme))
        {
            return null;
        }
        _cachedSchemes[scheme.Name] = scheme;
        if (typeof(IAuthenticationRequestHandler).IsAssignableFrom(scheme.HandlerType))
        {
            _cachedHandlerSchemes[scheme.Name] = scheme;
        }
        return scheme;
    }

    private async Task<DynamicAuthenticationScheme> GetDynamicSchemeAsync(string name)
    {
        if (_cachedSchemes.TryGetValue(name, out var cachedScheme))
        {
            return cachedScheme;
        }

        var scheme = await GetSchemeFromSsoConfigAsync(name);
        if (scheme == null)
        {
            return null;
        }

        await _semaphore.WaitAsync();
        try
        {
            scheme = SetSchemeInCache(scheme);
            if (scheme == null)
            {
                return null;
            }

            if (typeof(IAuthenticationRequestHandler).IsAssignableFrom(scheme.HandlerType))
            {
                _handlerSchemesCopy = _cachedHandlerSchemes.Values.ToArray();
            }
            _schemesCopy = _cachedSchemes.Values.ToArray();
        }
        finally
        {
            // Note: _lastSchemeLoad is not set here, this is a one-off
            //  and should not impact loading further cache updates
            _semaphore.Release();
        }
        return scheme;
    }

    private bool PostConfigureDynamicScheme(DynamicAuthenticationScheme scheme)
    {
        try
        {
            if (scheme.SsoType == SsoType.OpenIdConnect && scheme.Options is OpenIdConnectOptions oidcOptions)
            {
                _oidcPostConfigureOptions.PostConfigure(scheme.Name, oidcOptions);
                _extendedOidcOptionsMonitorCache.AddOrUpdate(scheme.Name, oidcOptions);
            }
            else if (scheme.SsoType == SsoType.Saml2 && scheme.Options is Saml2Options saml2Options)
            {
                _saml2PostConfigureOptions.PostConfigure(scheme.Name, saml2Options);
                _extendedSaml2OptionsMonitorCache.AddOrUpdate(scheme.Name, saml2Options);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing post configuration for '{0}' ({1})",
                scheme.Name, scheme.DisplayName);
        }
        return false;
    }

    private DynamicAuthenticationScheme GetSchemeFromSsoConfig(SsoConfig config)
    {
        var data = config.GetData();
        return data.ConfigType switch
        {
            SsoType.OpenIdConnect => GetOidcAuthenticationScheme(config.OrganizationId.ToString(), data),
            SsoType.Saml2 => GetSaml2AuthenticationScheme(config.OrganizationId.ToString(), data),
            _ => throw new Exception($"SSO Config Type, '{data.ConfigType}', not supported"),
        };
    }

    private async Task<DynamicAuthenticationScheme> GetSchemeFromSsoConfigAsync(string name)
    {
        if (!Guid.TryParse(name, out var organizationId))
        {
            _logger.LogWarning("Could not determine organization id from name, '{0}'", name);
            return null;
        }
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organizationId);
        if (ssoConfig == null || !ssoConfig.Enabled)
        {
            _logger.LogWarning("Could not find SSO config or config was not enabled for '{0}'", name);
            return null;
        }

        return GetSchemeFromSsoConfig(ssoConfig);
    }

    private DynamicAuthenticationScheme GetOidcAuthenticationScheme(string name, SsoConfigurationData config)
    {
        var oidcOptions = new OpenIdConnectOptions
        {
            Authority = config.Authority,
            ClientId = config.ClientId,
            ClientSecret = config.ClientSecret,
            ResponseType = "code",
            ResponseMode = "form_post",
            SignInScheme = AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme,
            SignOutScheme = IdentityServerConstants.SignoutScheme,
            SaveTokens = false, // reduce overall request size
            TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = JwtClaimTypes.Name,
                RoleClaimType = JwtClaimTypes.Role,
            },
            CallbackPath = SsoConfigurationData.BuildCallbackPath(),
            SignedOutCallbackPath = SsoConfigurationData.BuildSignedOutCallbackPath(),
            MetadataAddress = config.MetadataAddress,
            // Prevents URLs that go beyond 1024 characters which may break for some servers
            AuthenticationMethod = config.RedirectBehavior,
            GetClaimsFromUserInfoEndpoint = config.GetClaimsFromUserInfoEndpoint,
        };
        oidcOptions.Scope
            .AddIfNotExists(OpenIdConnectScopes.OpenId)
            .AddIfNotExists(OpenIdConnectScopes.Email)
            .AddIfNotExists(OpenIdConnectScopes.Profile);
        foreach (var scope in config.GetAdditionalScopes())
        {
            oidcOptions.Scope.AddIfNotExists(scope);
        }
        if (!string.IsNullOrWhiteSpace(config.ExpectedReturnAcrValue))
        {
            oidcOptions.Scope.AddIfNotExists(OpenIdConnectScopes.Acr);
        }

        oidcOptions.StateDataFormat = new DistributedCacheStateDataFormatter(_httpContextAccessor, name);

        // see: https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest (acr_values)
        if (!string.IsNullOrWhiteSpace(config.AcrValues))
        {
            oidcOptions.Events ??= new OpenIdConnectEvents();
            oidcOptions.Events.OnRedirectToIdentityProvider = ctx =>
            {
                ctx.ProtocolMessage.AcrValues = config.AcrValues;
                return Task.CompletedTask;
            };
        }

        return new DynamicAuthenticationScheme(name, name, typeof(OpenIdConnectHandler),
            oidcOptions, SsoType.OpenIdConnect);
    }

    private DynamicAuthenticationScheme GetSaml2AuthenticationScheme(string name, SsoConfigurationData config)
    {
        if (_samlEnvironment == null)
        {
            throw new Exception($"SSO SAML2 Service Provider profile is missing for {name}");
        }

        var spEntityId = new Sustainsys.Saml2.Metadata.EntityId(
            SsoConfigurationData.BuildSaml2ModulePath(_globalSettings.BaseServiceUri.Sso));
        bool? allowCreate = null;
        if (config.SpNameIdFormat != Saml2NameIdFormat.Transient)
        {
            allowCreate = true;
        }
        var spOptions = new SPOptions
        {
            EntityId = spEntityId,
            ModulePath = SsoConfigurationData.BuildSaml2ModulePath(null, name),
            NameIdPolicy = new Saml2NameIdPolicy(allowCreate, GetNameIdFormat(config.SpNameIdFormat)),
            WantAssertionsSigned = config.SpWantAssertionsSigned,
            AuthenticateRequestSigningBehavior = GetSigningBehavior(config.SpSigningBehavior),
            ValidateCertificates = config.SpValidateCertificates,
        };
        if (!string.IsNullOrWhiteSpace(config.SpMinIncomingSigningAlgorithm))
        {
            spOptions.MinIncomingSigningAlgorithm = config.SpMinIncomingSigningAlgorithm;
        }
        if (!string.IsNullOrWhiteSpace(config.SpOutboundSigningAlgorithm))
        {
            spOptions.OutboundSigningAlgorithm = config.SpOutboundSigningAlgorithm;
        }
        if (_samlEnvironment.SpSigningCertificate != null)
        {
            spOptions.ServiceCertificates.Add(_samlEnvironment.SpSigningCertificate);
        }

        var idpEntityId = new Sustainsys.Saml2.Metadata.EntityId(config.IdpEntityId);
        var idp = new Sustainsys.Saml2.IdentityProvider(idpEntityId, spOptions)
        {
            Binding = GetBindingType(config.IdpBindingType),
            AllowUnsolicitedAuthnResponse = config.IdpAllowUnsolicitedAuthnResponse,
            DisableOutboundLogoutRequests = config.IdpDisableOutboundLogoutRequests,
            WantAuthnRequestsSigned = config.IdpWantAuthnRequestsSigned,
        };
        if (!string.IsNullOrWhiteSpace(config.IdpSingleSignOnServiceUrl))
        {
            idp.SingleSignOnServiceUrl = new Uri(config.IdpSingleSignOnServiceUrl);
        }
        if (!string.IsNullOrWhiteSpace(config.IdpSingleLogoutServiceUrl))
        {
            idp.SingleLogoutServiceUrl = new Uri(config.IdpSingleLogoutServiceUrl);
        }
        if (!string.IsNullOrWhiteSpace(config.IdpOutboundSigningAlgorithm))
        {
            idp.OutboundSigningAlgorithm = config.IdpOutboundSigningAlgorithm;
        }
        if (!string.IsNullOrWhiteSpace(config.IdpX509PublicCert))
        {
            var cert = CoreHelpers.Base64UrlDecode(config.IdpX509PublicCert);
            idp.SigningKeys.AddConfiguredKey(new X509Certificate2(cert));
        }
        idp.ArtifactResolutionServiceUrls.Clear();
        // This must happen last since it calls Validate() internally.
        idp.LoadMetadata = false;

        var options = new Saml2Options
        {
            SPOptions = spOptions,
            SignInScheme = AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme,
            SignOutScheme = IdentityServerConstants.DefaultCookieAuthenticationScheme,
            CookieManager = new IdentityServer.DistributedCacheCookieManager(),
        };
        options.IdentityProviders.Add(idp);

        return new DynamicAuthenticationScheme(name, name, typeof(Saml2Handler), options, SsoType.Saml2);
    }

    private NameIdFormat GetNameIdFormat(Saml2NameIdFormat format)
    {
        return format switch
        {
            Saml2NameIdFormat.Unspecified => NameIdFormat.Unspecified,
            Saml2NameIdFormat.EmailAddress => NameIdFormat.EmailAddress,
            Saml2NameIdFormat.X509SubjectName => NameIdFormat.X509SubjectName,
            Saml2NameIdFormat.WindowsDomainQualifiedName => NameIdFormat.WindowsDomainQualifiedName,
            Saml2NameIdFormat.KerberosPrincipalName => NameIdFormat.KerberosPrincipalName,
            Saml2NameIdFormat.EntityIdentifier => NameIdFormat.EntityIdentifier,
            Saml2NameIdFormat.Persistent => NameIdFormat.Persistent,
            Saml2NameIdFormat.Transient => NameIdFormat.Transient,
            _ => NameIdFormat.NotConfigured,
        };
    }

    private SigningBehavior GetSigningBehavior(Saml2SigningBehavior behavior)
    {
        return behavior switch
        {
            Saml2SigningBehavior.IfIdpWantAuthnRequestsSigned => SigningBehavior.IfIdpWantAuthnRequestsSigned,
            Saml2SigningBehavior.Always => SigningBehavior.Always,
            Saml2SigningBehavior.Never => SigningBehavior.Never,
            _ => SigningBehavior.IfIdpWantAuthnRequestsSigned,
        };
    }

    private Sustainsys.Saml2.WebSso.Saml2BindingType GetBindingType(Saml2BindingType bindingType)
    {
        return bindingType switch
        {
            Saml2BindingType.HttpRedirect => Sustainsys.Saml2.WebSso.Saml2BindingType.HttpRedirect,
            Saml2BindingType.HttpPost => Sustainsys.Saml2.WebSso.Saml2BindingType.HttpPost,
            _ => Sustainsys.Saml2.WebSso.Saml2BindingType.HttpPost,
        };
    }
}
