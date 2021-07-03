using System;
using Bit.Core.Models.Mail;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.Core.Utilities
{
    public class MailQueueMessageConverter : CircularJsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(IMailQueueMessage).IsAssignableFrom(objectType);
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var result = existingValue;

            RemoveConverterAndAct(serializer, () =>
            {
                JObject o = JObject.Load(reader);
                var modelTypeName = CoreHelpers.GetJProperty(o, nameof(IMailQueueMessage.ModelType));
                if (modelTypeName != null)
                {
                    Type modelType = Type.GetType(modelTypeName.Value.ToString());
                    Type messageType = typeof(MailQueueMessage<>).MakeGenericType(modelType);
                    result = o.ToObject(messageType, serializer);
                }
                else
                {
                    result = o.ToObject(typeof(MailQueueMessage), serializer);
                }
            });

            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            RemoveConverterAndAct(serializer, () =>
            {
                serializer.Serialize(writer, value);
            });
        }
    }
}
