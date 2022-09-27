namespace Bit.Core.Models.Data;

public abstract class SendData
{
    public SendData() { }

    public SendData(string name, string notes)
    {
        Name = name;
        Notes = notes;
    }

    public string Name { get; set; }
    public string Notes { get; set; }
}
