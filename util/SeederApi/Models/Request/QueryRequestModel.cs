using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Bit.SeederApi.Models.Requests;

public class QueryRequestModel
{
    [Required]
    public required string Template { get; set; }
    public JsonElement? Arguments { get; set; }
}