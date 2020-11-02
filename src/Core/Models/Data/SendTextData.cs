using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class SendTextData : SendData
    {
        public SendTextData() { }

        public SendTextData(SendRequestModel send)
            : base(send)
        {
            Text = send.Text.Text;
            Hidden = send.Text.Hidden;
        }

        public string Text { get; set; }
        public bool Hidden { get; set; }
    }
}
