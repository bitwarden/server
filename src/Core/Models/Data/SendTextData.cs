namespace Bit.Core.Models.Data;

public class SendTextData : SendData
{
    public SendTextData() { }

    public SendTextData(string name, string notes, string text, bool hidden)
        : base(name, notes)
    {
        Text = text;
        Hidden = hidden;
    }

    public string Text { get; set; }
    public bool Hidden { get; set; }
}
