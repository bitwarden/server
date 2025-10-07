namespace Bit.Infrastructure.EntityFramework.Models;

public class SeededData
{
    public Guid Id { get; set; }
    public required string RecipeName { get; set; }
    public required string Data { get; set; } // JSON blob with entity tracking info
    public DateTime CreationDate { get; set; }
}
