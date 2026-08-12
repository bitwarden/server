using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// See FeatureManager.cs — IInteractionService is still behind Aspire's experimental diagnostic.
#pragma warning disable ASPIREINTERACTION001

namespace Bit.AppHost;

/// <summary>
/// A SCIM request gathered from the dashboard prompt.
/// </summary>
internal sealed record ScimUserRequest(
    string ApiKey,
    Guid OrganizationId,
    string Operation,
    string Email,
    string ExternalId,
    string DisplayName);

/// <summary>
/// The connection details from the last SCIM request, so they only have to be typed once.
/// </summary>
/// <remarks>
/// Deliberately in-memory only, for the lifetime of this app host process. The API key is never
/// written to disk — a restart asks for it again.
/// </remarks>
internal sealed class ScimClientState
{
    public string ApiKey { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;
}

/// <summary>
/// A minimal SCIM client for the locally running Scim service, so provisioning flows can be exercised
/// without reaching for curl or a real IdP.
/// </summary>
/// <remarks>
/// Talks to <c>bitwarden_license/src/Scim</c>'s <c>UsersController</c> (<c>v2/{organizationId}/users</c>).
/// Update and disable both resolve the user by <c>externalId</c> first, so the prompt only ever needs an
/// email and an external ID — never an internal organization-user GUID.
/// </remarks>
internal static class ScimClient
{
    private const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ScimMediaType = "application/scim+json";

    /// <summary>Adds the highlighted "SCIM client" command to the Scim service resource.</summary>
    public static IResourceBuilder<ProjectResource> WithScimClient(this IResourceBuilder<ProjectResource> scim)
    {
        var endpoint = scim.GetEndpoint("http");
        var state = new ScimClientState();

        return scim.WithCommand(
            name: "scim-client",
            displayName: "SCIM client",
            executeCommand: context => ExecuteAsync(context, endpoint, state),
            commandOptions: new CommandOptions
            {
                Description = "Send a SCIM create, update or disable request to this server.",
                IconName = "PersonSync",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                UpdateState = _ => ResourceCommandState.Enabled
            });
    }

    private static async Task<ExecuteCommandResult> ExecuteAsync(
        ExecuteCommandContext context,
        EndpointReference endpoint,
        ScimClientState state)
    {
        var cancellationToken = context.CancellationToken;
        var interaction = context.ServiceProvider.GetRequiredService<IInteractionService>();

        if (!interaction.IsAvailable)
        {
            return CommandResults.Failure("The SCIM client needs the Aspire dashboard to prompt for input.");
        }

        if (!endpoint.IsAllocated)
        {
            return CommandResults.Failure("The scim service is not running — start it and try again.");
        }

        var request = await PromptAsync(interaction, state, cancellationToken);
        if (request is null)
        {
            return CommandResults.Canceled();
        }

        // Remembered for the next invocation in this session only.
        state.ApiKey = request.ApiKey;
        state.OrganizationId = request.OrganizationId.ToString();

        using var http = new HttpClient { BaseAddress = new Uri(endpoint.Url) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ScimMediaType));

        try
        {
            return request.Operation switch
            {
                "create" => await CreateAsync(http, request, context, cancellationToken),
                "update" => await SetActiveAsync(http, request, active: true, context, cancellationToken),
                _ => await SetActiveAsync(http, request, active: false, context, cancellationToken)
            };
        }
        catch (HttpRequestException exception)
        {
            return CommandResults.Failure($"Could not reach the scim service at {endpoint.Url}: {exception.Message}");
        }
    }

    private static async Task<ScimUserRequest?> PromptAsync(
        IInteractionService interaction,
        ScimClientState state,
        CancellationToken cancellationToken)
    {
        var result = await interaction.PromptInputsAsync(
            "SCIM client",
            "Sends a SCIM 2.0 request to this server. Update and disable look the user up by external ID.",
            [
                new InteractionInput
                {
                    Name = "apiKey",
                    Label = "SCIM API key",
                    InputType = InputType.SecretText,
                    Required = true,
                    Value = state.ApiKey,
                    Description = "From the organization's SCIM settings. Sent as a bearer token. "
                        + "Kept for this session only."
                },
                new InteractionInput
                {
                    Name = "organizationId",
                    Label = "Organization ID",
                    InputType = InputType.Text,
                    Required = true,
                    Value = state.OrganizationId,
                    Placeholder = "00000000-0000-0000-0000-000000000000"
                },
                new InteractionInput
                {
                    Name = "operation",
                    Label = "Operation",
                    InputType = InputType.Choice,
                    Value = "create",
                    Options =
                    [
                        new("create", "Create — invite a new member"),
                        new("update", "Update — re-send details, restore if revoked"),
                        new("disable", "Disable — revoke the member")
                    ]
                },
                new InteractionInput
                {
                    Name = "email",
                    Label = "Email",
                    InputType = InputType.Text,
                    Required = true,
                    Placeholder = "jane.doe@example.com",
                    Description = "Used as both userName and the primary work email."
                },
                new InteractionInput
                {
                    Name = "externalId",
                    Label = "External ID",
                    InputType = InputType.Text,
                    Required = true,
                    Description = "The IdP's stable identifier. Update and disable resolve the user by this."
                },
                new InteractionInput
                {
                    Name = "displayName",
                    Label = "Display name",
                    InputType = InputType.Text,
                    Description = "Optional — derived from the email when left blank."
                }
            ],
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Send",
                ValidationCallback = validation =>
                {
                    if (!Guid.TryParse(validation.Inputs["organizationId"].Value, out _))
                    {
                        validation.AddValidationError(
                            validation.Inputs["organizationId"], "Must be a GUID.");
                    }

                    var email = validation.Inputs["email"].Value;
                    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    {
                        validation.AddValidationError(validation.Inputs["email"], "Must be a valid email address.");
                    }

                    if (string.IsNullOrWhiteSpace(validation.Inputs["externalId"].Value))
                    {
                        validation.AddValidationError(validation.Inputs["externalId"], "External ID is required.");
                    }

                    return Task.CompletedTask;
                }
            },
            cancellationToken);

        if (result.Canceled)
        {
            return null;
        }

        var inputs = result.Data;
        var address = inputs["email"].Value!.Trim();

        return new ScimUserRequest(
            ApiKey: inputs["apiKey"].Value!.Trim(),
            OrganizationId: Guid.Parse(inputs["organizationId"].Value!),
            Operation: inputs["operation"].Value ?? "create",
            Email: address,
            ExternalId: inputs["externalId"].Value!.Trim(),
            DisplayName: string.IsNullOrWhiteSpace(inputs["displayName"].Value)
                ? DeriveDisplayName(address)
                : inputs["displayName"].Value!.Trim());
    }

    private static async Task<ExecuteCommandResult> CreateAsync(
        HttpClient http,
        ScimUserRequest request,
        ExecuteCommandContext context,
        CancellationToken cancellationToken)
    {
        // The server rejects a create whose payload is inactive, so this is always true on POST.
        var payload = BuildUser(request, active: true);
        var response = await http.PostAsync(
            $"/v2/{request.OrganizationId}/users", Body(payload), cancellationToken);

        return await DescribeAsync(response, $"Created {request.Email}", request, context, cancellationToken);
    }

    private static async Task<ExecuteCommandResult> SetActiveAsync(
        HttpClient http,
        ScimUserRequest request,
        bool active,
        ExecuteCommandContext context,
        CancellationToken cancellationToken)
    {
        var (userId, lookupFailure) = await ResolveByExternalIdAsync(http, request, cancellationToken);
        if (lookupFailure is not null)
        {
            return lookupFailure;
        }

        var payload = BuildUser(request, active);
        var response = await http.PutAsync(
            $"/v2/{request.OrganizationId}/users/{userId}", Body(payload), cancellationToken);

        var verb = active ? "Updated" : "Disabled";
        return await DescribeAsync(response, $"{verb} {request.Email}", request, context, cancellationToken);
    }

    /// <summary>
    /// Finds the organization user's ID via <c>filter=externalId eq "…"</c>, which the server's
    /// <c>GetUsersListQuery</c> understands, so the prompt never has to ask for an internal GUID.
    /// </summary>
    private static async Task<(string? UserId, ExecuteCommandResult? Failure)> ResolveByExternalIdAsync(
        HttpClient http,
        ScimUserRequest request,
        CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString($"externalId eq \"{request.ExternalId}\"");
        var response = await http.GetAsync(
            $"/v2/{request.OrganizationId}/users?filter={filter}", cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (null, CommandResults.Failure(
                $"Lookup failed: {Explain(response.StatusCode)}", Pretty(body), CommandResultFormat.Json));
        }

        var id = JsonNode.Parse(body)?["Resources"]?.AsArray().FirstOrDefault()?["id"]?.GetValue<string>();

        return id is null
            ? (null, CommandResults.Failure(
                $"No member with external ID '{request.ExternalId}' in that organization.",
                Pretty(body),
                CommandResultFormat.Json))
            : (id, null);
    }

    private static JsonObject BuildUser(ScimUserRequest request, bool active)
    {
        var (givenName, familyName) = SplitName(request.DisplayName);

        return new JsonObject
        {
            ["schemas"] = new JsonArray(UserSchema),
            ["userName"] = request.Email,
            ["displayName"] = request.DisplayName,
            ["externalId"] = request.ExternalId,
            ["active"] = active,
            ["name"] = new JsonObject
            {
                ["formatted"] = request.DisplayName,
                ["givenName"] = givenName,
                ["familyName"] = familyName
            },
            ["emails"] = new JsonArray(
                new JsonObject
                {
                    ["primary"] = true,
                    ["value"] = request.Email,
                    ["type"] = "work"
                })
        };
    }

    private static StringContent Body(JsonObject payload) =>
        new(payload.ToJsonString(), Encoding.UTF8, ScimMediaType);

    private static async Task<ExecuteCommandResult> DescribeAsync(
        HttpResponseMessage response,
        string successMessage,
        ScimUserRequest request,
        ExecuteCommandContext context,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        context.Logger.LogInformation(
            "SCIM {Operation} for {ExternalId} returned {StatusCode}.",
            request.Operation, request.ExternalId, (int)response.StatusCode);

        // Disable and PATCH-style operations legitimately answer 204 with an empty body.
        var detail = string.IsNullOrWhiteSpace(body) ? "{}" : Pretty(body);

        return response.IsSuccessStatusCode
            ? CommandResults.Success($"{successMessage} ({(int)response.StatusCode})", detail, CommandResultFormat.Json)
            : CommandResults.Failure(Explain(response.StatusCode), detail, CommandResultFormat.Json);
    }

    private static string Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized =>
            "401 Unauthorized — check the API key, and that the organization has SCIM enabled with an "
            + "enabled SCIM configuration.",
        HttpStatusCode.NotFound => "404 Not Found — no such organization or member.",
        HttpStatusCode.Conflict => "409 Conflict — that user is already a member.",
        HttpStatusCode.UnsupportedMediaType =>
            $"415 Unsupported Media Type — the server rejected '{ScimMediaType}'.",
        _ => $"{(int)status} {status}."
    };

    private static string Pretty(string body)
    {
        try
        {
            return JsonSerializer.Serialize(
                JsonNode.Parse(body), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return body;
        }
    }

    /// <summary>Turns <c>jane.doe@example.com</c> into <c>Jane Doe</c>.</summary>
    private static string DeriveDisplayName(string email)
    {
        var local = email.Split('@')[0];
        var parts = local.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 0 ? local : string.Join(' ', parts.Select(Capitalise));
    }

    private static (string GivenName, string FamilyName) SplitName(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => (displayName, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], string.Join(' ', parts[1..]))
        };
    }

    private static string Capitalise(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
