// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class OnyxAnswerWithCitationResponseModel
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; }

    [JsonPropertyName("rephrase")]
    public string Rephrase { get; set; }

    [JsonPropertyName("citations")]
    public List<Citation> Citations { get; set; }

    [JsonPropertyName("llm_selected_doc_indices")]
    public List<int> LlmSelectedDocIndices { get; set; }

    [JsonPropertyName("error_msg")]
    public string ErrorMsg { get; set; }
}

public class Citation
{
    [JsonPropertyName("citation_num")]
    public int CitationNum { get; set; }

    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; }
}
