namespace Bit.Core.Enums;

public enum IntegrationType : int
{
    CloudBillingSync = 1,
    Scim = 2,
    Slack = 3,
    Webhook = 4,
}

public static class IntegrationTypeExtensions
{
    public static string ToRoutingKey(this IntegrationType type)
    {
        switch (type)
        {
            case IntegrationType.Slack:
                return "slack";
            case IntegrationType.Webhook:
                return "webhook";
            default:
                throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported integration type: {type}");
        }
    }
}
