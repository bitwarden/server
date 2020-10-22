using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class SendAccessResponseModel : ResponseModel
    {
        public SendAccessResponseModel(Send send, GlobalSettings globalSettings)
            : base("send-access")
        {
            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            Id = send.Id.ToString();
            Type = send.Type;

            SendData sendData;
            switch (send.Type)
            {
                case SendType.File:
                    var fileData = JsonConvert.DeserializeObject<SendFileData>(send.Data);
                    sendData = fileData;
                    File = new SendFileModel(fileData, globalSettings);
                    break;
                case SendType.Text:
                    var textData = JsonConvert.DeserializeObject<SendTextData>(send.Data);
                    sendData = textData;
                    Text = new SendTextModel(textData);
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }

            Name = sendData.Name;
        }

        public string Id { get; set; }
        public SendType Type { get; set; }
        public string Name { get; set; }
        public SendFileModel File { get; set; }
        public SendTextModel Text { get; set; }
    }
}
