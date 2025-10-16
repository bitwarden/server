namespace Bit.Infrastructure.EntityFramework.Models;

public class SeededData
{
    public Guid Id { get; set; }
    public required string RecipeName { get; set; }
    /// <summary>
    /// JSON blob containing all
    /// </summary>
    public required string Data { get; set; }
    public DateTime CreationDate { get; set; }
}
