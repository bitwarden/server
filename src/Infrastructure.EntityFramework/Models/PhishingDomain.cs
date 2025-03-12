using System.ComponentModel.DataAnnotations;

namespace Bit.Infrastructure.EntityFramework.Models;

public class PhishingDomain
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Domain { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
