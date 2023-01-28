using Microsoft.Azure.Documents;
using Newtonsoft.Json.Linq;

namespace Bit.Admin.Models;

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
    public JObject Exception { get; set; }

    public string ExceptionToString(JObject e)
    {
        if (e == null)
        {
            return null;
        }

        var val = string.Empty;
        if (e["Message"] != null && e["Message"].ToObject<string>() != null)
        {
            val += "Message:\n";
            val += e["Message"] + "\n";
        }

        if (e["StackTrace"] != null && e["StackTrace"].ToObject<string>() != null)
        {
            val += "\nStack Trace:\n";
            val += e["StackTrace"];
        }
        else if (e["StackTraceString"] != null && e["StackTraceString"].ToObject<string>() != null)
        {
            val += "\nStack Trace String:\n";
            val += e["StackTraceString"];
        }

        if (e["InnerException"] != null && e["InnerException"].ToObject<JObject>() != null)
        {
            val += "\n\n=== Inner Exception ===\n\n";
            val += ExceptionToString(e["InnerException"].ToObject<JObject>());
        }

        return val;
    }
}
