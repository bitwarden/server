using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Auth.Authorization.Requirements;

// <summary>
// Requires that the id of the send request matches the id of the subject claim in the send access token.
// </summary>
public class SameSendIdRequirement : IAuthorizationRequirement { }
