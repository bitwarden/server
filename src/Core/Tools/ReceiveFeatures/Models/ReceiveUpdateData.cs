namespace Bit.Core.Tools.ReceiveFeatures.Models;

public class ReceiveUpdateData
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTime ExpirationDate { get; init; }
}

