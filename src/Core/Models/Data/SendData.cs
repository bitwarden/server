using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public abstract class SendData
    {
        public SendData() { }

        public SendData(SendRequestModel send)
        {
            Name = send.Name;
            Notes = send.Notes;
        }

        public string Name { get; set; }
        public string Notes { get; set; }
    }
}
