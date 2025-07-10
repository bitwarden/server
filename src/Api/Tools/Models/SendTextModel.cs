﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models;

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
