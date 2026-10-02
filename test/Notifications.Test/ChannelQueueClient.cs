using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Azure;
using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Notifications.Test;

/// <summary>
/// An in-memory <see cref="QueueClient"/> backed by a <see cref="Channel{T}"/> so tests can drive
/// both <see cref="Bit.Notifications.AzureQueueHostedService"/> (consumer) and
/// <see cref="Bit.Core.Platform.Push.Internal.AzureQueuePushEngine"/> (producer) without any real
/// Azure Storage dependency.
///
/// <para><see cref="ReceiveMessagesAsync"/> blocks until a message is available rather than
/// returning an empty array, so the hosted service never enters its 5-second sleep path during
/// tests.</para>
/// </summary>
internal sealed class ChannelQueueClient : QueueClient
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

    // Captures every message written via SendMessageAsync so tests can inspect what
    // AzureQueuePushEngine produced without consuming from the receive-side channel.
    private readonly Channel<string> _sentCaptures = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

    /// <summary>
    /// Waits for the next message written via <see cref="SendMessageAsync"/> and returns it.
    /// Use this in tests to capture what <c>AzureQueuePushEngine</c> wrote to the queue without
    /// consuming from the receive-side channel that the hosted service reads.
    /// </summary>
    internal async Task<string> AwaitNextSentAsync(CancellationToken cancellationToken = default)
    {
        await _sentCaptures.Reader.WaitToReadAsync(cancellationToken);
        _sentCaptures.Reader.TryRead(out var text);
        return text!;
    }

    public override Task<Response<SendReceipt>> SendMessageAsync(
        string messageText,
        TimeSpan? visibilityTimeout = null,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryWrite(messageText);
        _sentCaptures.Writer.TryWrite(messageText);
        var receipt = QueuesModelFactory.SendReceipt(
            messageId: Guid.NewGuid().ToString(),
            insertionTime: DateTimeOffset.UtcNow,
            expirationTime: DateTimeOffset.UtcNow.AddDays(7),
            popReceipt: "pop",
            timeNextVisible: DateTimeOffset.UtcNow);
        return Task.FromResult(Response.FromValue(receipt, new FakeResponse()));
    }

    public override async Task<Response<QueueMessage[]>> ReceiveMessagesAsync(
        int? maxMessages = null,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var batch = new List<QueueMessage>();
        var limit = maxMessages ?? 32;

        // Block until at least one message arrives or cancellation is requested.
        if (!await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            return Response.FromValue(Array.Empty<QueueMessage>(), new FakeResponse());
        }

        while (batch.Count < limit && _channel.Reader.TryRead(out var text))
        {
            batch.Add(QueuesModelFactory.QueueMessage(
                messageId: Guid.NewGuid().ToString(),
                popReceipt: "pop",
                messageText: text,
                dequeueCount: 1,
                nextVisibleOn: DateTimeOffset.UtcNow.AddMinutes(1),
                insertedOn: DateTimeOffset.UtcNow,
                expiresOn: DateTimeOffset.UtcNow.AddDays(7)));
        }

        return Response.FromValue(batch.ToArray(), new FakeResponse());
    }

    public override Task<Response> DeleteMessageAsync(
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Response>(new FakeResponse());

    private sealed class FakeResponse : Response
    {
        public override int Status => 200;
        public override string ReasonPhrase => "OK";
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = string.Empty;
        public override void Dispose() { }

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }

        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];
    }
}
