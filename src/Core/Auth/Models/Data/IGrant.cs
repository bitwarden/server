namespace Bit.Core.Auth.Models.Data;

public interface IGrant
{
    string Key { get; set; }
    string Type { get; set; }
    string SubjectId { get; set; }
    string SessionId { get; set; }
    string ClientId { get; set; }
    string Description { get; set; }
    DateTime CreationDate { get; set; }
    DateTime? ExpirationDate { get; set; }
    DateTime? ConsumedDate { get; set; }
    string Data { get; set; }
}
