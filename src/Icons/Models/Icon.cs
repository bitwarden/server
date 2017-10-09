using System;

namespace Icons.Models
{
    [Serializable]
    public class Icon
    {
        public byte[] Image { get; }

        public string Format { get; }

        public Icon(byte[] image, string format)
        {
            this.Image = image;
            this.Format = format;
        }
    }
}
