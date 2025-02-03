namespace Bit.Billing.Models;

public class OnyxAnswerWithCitationResponseModel
{
    public string Answer { get; set; }
    public string Rephrase { get; set; }
    public List<Citation> Citations { get; set; }
    public List<int> LlmSelectedDocIndices { get; set; }
    public string ErrorMsg { get; set; }
}

public class Citation
{
    public int CitationNum { get; set; }
    public string DocumentId { get; set; }
}
