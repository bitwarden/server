using System.ComponentModel.DataAnnotations;

public class RevokeAccessTokensRequest
{
    [Required]
    public Guid[] Ids { get; set; }
}
