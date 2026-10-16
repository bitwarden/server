using Bit.Api.AdminConsole.Partnerships.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Idempotency;

/// <summary>
/// Opts a partner write action into <c>Idempotency-Key</c> handling. To opt in, add <c>[PartnershipIdempotent]</c> to
/// the POST action on a <see cref="PartnershipControllerBase"/> subclass; nothing else is needed.
/// </summary>
/// <remarks>
/// <para>
/// The header is optional. When present it must be 1 to 255 printable ASCII characters, otherwise the request gets 400
/// <see cref="PartnershipErrorResults.InvalidIdempotencyKeyCode"/>. Keys are scoped to the partnership.
/// </para>
/// <para>
/// The first request runs the action and, for 2xx and 4xx other than 429, stores its status, content type, and body
/// for 24 hours alongside a hash of the method, path, query, and body. A repeat of the same request returns the stored
/// response without running the action and adds <c>Idempotent-Replayed: true</c>; a replayed error body keeps the
/// original <c>requestId</c>. Other response headers are not stored. The same key with a different request gets 409
/// <see cref="PartnershipErrorResults.IdempotencyKeyReusedCode"/>.
/// </para>
/// <para>
/// While the first request runs, the same key gets 409
/// <see cref="PartnershipErrorResults.IdempotencyKeyInProgressCode"/>. The hold ends when the request finishes or after
/// <see cref="PartnershipIdempotencyStore.InFlightLifetime"/>. 5xx, 429, and unhandled exceptions release the key so
/// the partner can retry with it. See <see cref="PartnershipIdempotencyStore"/> for the cross-instance limit.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PartnershipIdempotentAttribute() : TypeFilterAttribute(typeof(PartnershipIdempotencyFilter));
