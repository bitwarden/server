using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Bit.SeederApi.Models.Request;

public class SeedRequestModel
{
    [Required]
    public required string Template { get; set; }
    public JsonElement? Arguments { get; set; }
}
