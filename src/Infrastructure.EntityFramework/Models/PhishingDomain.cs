using System.ComponentModel.DataAnnotations;

namespace Bit.Infrastructure.EntityFramework.Models;

public class PhishingDomain
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Domain { get; set; }

    [MaxLength(64)]
    public string Checksum { get; set; }
}
