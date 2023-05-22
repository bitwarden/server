using System.Text.Json.Serialization;

namespace Bit.Core.Models.Api.Response.Duo;

public class DuoResponseModel
{
    [JsonPropertyName("stat")]
    public string Stat { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("message_detail")]
    public string MessageDetail { get; set; }

    [JsonPropertyName("response")]
    public Response Response { get; set; }
}

public class Response
{
    [JsonPropertyName("time")]
    public int Time { get; set; }
}
