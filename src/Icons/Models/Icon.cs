using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Icons.Models
{
    [Serializable]
    public class Icon
    {

        public byte[] Image { get; }

        public string Format { get; }
        
        public DateTime CreatedAt { get; }

        public Icon(byte[] image, string format)
        {
            this.Image = image;
            this.Format = format;
            this.CreatedAt = DateTime.Now;
        }

        public bool HasNotExpired()
        {
            return CreatedAt > DateTime.Now.AddDays(-1);
        }
    }
}
