using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models;

public class SendTextModel
{
    public SendTextModel() { }

    public SendTextModel(SendTextData data)
    {
        Text = data.Text;
        Hidden = data.Hidden;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Text { get; set; }
    public bool Hidden { get; set; }
}
