// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

public class RevokeAccessTokensRequest
{
    [Required]
    public Guid[] Ids { get; set; }
}
