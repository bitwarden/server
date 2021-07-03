using System;
using System.Linq;
using Newtonsoft.Json;

namespace Bit.Core.Utilities
{
    public abstract class CircularJsonConverter : JsonConverter
    {
        public void RemoveConverterAndAct(JsonSerializer serializer, Action action)
        {
            var (converter, index) = serializer.Converters.Select((c, i) => (c, i)).FirstOrDefault(t => t.Item1.GetType() == this.GetType());
            serializer.Converters.RemoveAt(index);

            action();

            if (converter != null)
            {
                serializer.Converters.Insert(index, converter);
            }
        }
    }
}
