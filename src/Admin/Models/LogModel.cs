using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Bit.Core.Utilities;
using Microsoft.Azure.Documents;

namespace Bit.Admin.Models
{
    public class LogModel : Resource
    {
        public long EventIdHash { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string MessageTruncated => Message.Length > 200 ? $"{Message.Substring(0, 200)}..." : Message;
        public string MessageTemplate { get; set; }
        public IDictionary<string, object> Properties { get; set; }
        public string Project => Properties?.ContainsKey("Project") ?? false ? Properties["Project"].ToString() : null;
    }

    public class LogDetailsModel : LogModel
    {
        public JsonDocument Exception { get; set; }

        public string ExceptionToString(JsonDocument e)
        {
            if (e == null)
            {
                return null;
            }

            var root = e.RootElement;

            var sb = new StringBuilder();
            if (root.TryGetProperty("Message", out var messageProp) && messageProp.GetString() != null)
            {
                sb.AppendLine("Message:");
                sb.AppendLine(messageProp.GetString());
            }

            if (root.TryGetProperty("StackTrace", out var stackTraceProp) && stackTraceProp.GetString() != null)
            {
                sb.AppendLine();
                sb.AppendLine("Stack Trace:");
                sb.Append(stackTraceProp.GetString());
            }
            else if (root.TryGetProperty("StackTraceString", out var stackTraceStringProp) && stackTraceStringProp.GetString() != null)
            {
                sb.AppendLine();
                sb.AppendLine("Stack Trace String:");
                sb.Append(stackTraceStringProp.GetString());
            }

            if (root.TryGetProperty("InnerException", out var innerExceptionProp) && innerExceptionProp.ValueKind == JsonValueKind.Object)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("=== Inner Exception ===");
                sb.AppendLine();
                sb.AppendLine(ExceptionToString(innerExceptionProp.ToObject<JsonDocument>()));
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
