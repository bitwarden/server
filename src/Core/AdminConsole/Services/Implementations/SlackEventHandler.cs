using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class SlackEventHandler(
    GlobalSettings globalSettings,
    SlackMessageSender slackMessageSender)
    : IEventMessageHandler
{
    private readonly string _token = globalSettings.EventLogging.SlackToken;
    private readonly string _email = globalSettings.EventLogging.SlackUserEmail;

    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        await slackMessageSender.SendDirectMessageByEmailAsync(
            _token,
            JsonSerializer.Serialize(eventMessage),
            _email
        );
    }

    public async Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages)
    {
        await slackMessageSender.SendDirectMessageByEmailAsync(
            _token,
            JsonSerializer.Serialize(eventMessages),
            _email
        );
    }
}
