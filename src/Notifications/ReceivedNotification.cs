using System.Text.Json;
using Bit.Core.Enums;

namespace Bit.Notifications;

/// <summary>
/// A notification as this service receives it, from either the Azure Queue or <c>POST /send</c>.
/// </summary>
/// <remarks>
/// Deliberately narrower than <see cref="Bit.Core.Models.PushNotificationData{T}"/>, the envelope
/// senders write: that envelope also carries the target a notification is bound for, which exists to
/// get it to the right connections and is no business of clients. Leaving those properties off here
/// drops them, since unmapped JSON is ignored.
///
/// <para>The payload stays as raw JSON until the push type says what to read it as. Routing still
/// reads the destination out of it; once no sender can be running that omits the envelope's routing
/// fields, this type can take those instead and routing can use them directly, which is the point of
/// sending them.</para>
///
/// <para><strong><see cref="PushType.AuthRequestResponse"/> is an exception to that, and switching it
/// to envelope routing would break passwordless login.</strong> Its envelope says the target is the
/// requesting user, which is what the mobile engine needs for its notification hub tags, but it has
/// to be delivered to the anonymous hub grouped by the auth request's own id -- a value that exists
/// only in the payload. It stays payload-driven unless the envelope gains a field for it.</para>
///
/// <para><strong>Routing off the envelope is not the same as forwarding the payload untouched, and
/// the second one is not available yet.</strong> Reading the payload into its real type is what
/// normalises it: a sender deployed before this release posts camelCase to <c>/send</c>, and clients
/// read the PascalCase property names of the CLR types, so handing that JSON straight to SignalR
/// would reach those clients as properties they cannot find. That only becomes safe a release after
/// both ingresses produce PascalCase, at which point the superseded formats can go too.</para>
///
/// <para>Casing is not the only obstacle. Deserializing gives clients a
/// <see cref="System.DateTime"/> encoded as a MessagePack timestamp; raw JSON would give them a
/// string. A pass-through therefore also needs a resolver that re-encodes the values SignalR
/// currently gets for free from the CLR types.</para>
/// </remarks>
public class ReceivedNotification
{
    public PushType Type { get; set; }
    public JsonElement Payload { get; set; }
    public string? ContextId { get; set; }

    /// <summary>
    /// Reads the payload as <typeparamref name="T"/> and returns the notification in the shape
    /// clients receive, or <see langword="null"/> when the payload does not parse as that type.
    /// </summary>
    public ClientNotification<T>? ForClients<T>(JsonSerializerOptions options)
    {
        var payload = Payload.Deserialize<T>(options);
        return payload is null
            ? null
            : new ClientNotification<T> { Type = Type, Payload = payload, ContextId = ContextId };
    }
}
