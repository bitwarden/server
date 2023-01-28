using Bit.Core.Enums;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.WebSso;

namespace Bit.Sso.Controllers;

public class MetadataController : Controller
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    public MetadataController(
        IAuthenticationSchemeProvider schemeProvider)
    {
        _schemeProvider = schemeProvider;
    }

    [HttpGet("saml2/{scheme}")]
    public async Task<IActionResult> ViewAsync(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return NotFound();
        }

        var authScheme = await _schemeProvider.GetSchemeAsync(scheme);
        if (authScheme == null ||
            !(authScheme is DynamicAuthenticationScheme dynamicAuthScheme) ||
            dynamicAuthScheme?.SsoType != SsoType.Saml2)
        {
            return NotFound();
        }

        if (!(dynamicAuthScheme.Options is Saml2Options options))
        {
            return NotFound();
        }

        var uri = new Uri(
            Request.Scheme
            + "://"
            + Request.Host
            + Request.Path
            + Request.QueryString);

        var pathBase = Request.PathBase.Value;
        pathBase = string.IsNullOrEmpty(pathBase) ? "/" : pathBase;

        var requestdata = new HttpRequestData(
            Request.Method,
            uri,
            pathBase,
            null,
            Request.Cookies,
            (data) => data);

        var metadataResult = CommandFactory
            .GetCommand(CommandFactory.MetadataCommand)
            .Run(requestdata, options);
        //Response.Headers.Add("Content-Disposition", $"filename= bitwarden-saml2-meta-{scheme}.xml");
        return new ContentResult
        {
            Content = metadataResult.Content,
            ContentType = "text/xml",
        };
    }
}
