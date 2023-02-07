using System.Text.Json.Serialization;

namespace Bit.Core.Models.Api.Response.Duo;

public class DuoResponseModel
{
    [JsonPropertyName("stat")]
    public string Stat { get; set; }
    
    [JsonPropertyName("response")]
    public Response Response { get; set; }
}

public class Response
{
    [JsonPropertyName("time")]
    public int Time { get; set; }
}
