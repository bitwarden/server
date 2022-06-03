using System.Collections.Generic;
using System.IO;
using System.Linq;
using HandlebarsDotNet;

namespace Bit.Core.Utilities
{
    public static class HandlebarsLinkHelper
    {
        public static void Helper(TextWriter writer, dynamic context, params object[] parameters)
        {
            if (parameters.Length == 0)
            {
                writer.WriteSafeString(string.Empty);
                return;
            }

            var text = parameters[0].ToString();
            var parsedParams = ParseOverrideParameters(parameters.Skip(1).ToArray());
            var href = parsedParams.href ?? text;
            var clickTrackingOff = parsedParams.clickTrackingOff ?? false;

            var clickTrackingText = clickTrackingOff ? "clicktracking=off" : string.Empty;
            writer.WriteSafeString($"<a href=\"{href}\" target=\"_blank\" {clickTrackingText}>{text}</a>");
        }

        private static (string href, bool? clickTrackingOff) ParseOverrideParameters(object[] parameters)
        {
            if (parameters.Length < 1)
            {
                return (null, null);
            }
            else if (parameters.Length == 1)
            {
                return ParseSingleOverrideParameter(parameters[0]);
            }
            else
            {
                return ParseMultipleOverrideParameters(parameters);
            }
        }

        private static (string href, bool? clickTrackingOff) ParseSingleOverrideParameter(object parameter)
        {
            var (isBool, val) = TryBool(parameter);
            if (isBool)
            {
                return (null, val);
            }
            else if (parameter is string str)
            {
                return (str, null);
            }
            return (null, null);
        }

        private static (string href, bool? clickTrackingOff) ParseMultipleOverrideParameters(object[] parameters)
        {
            string href = null;
            bool? clickTrackingOff = null;

            if (parameters[0] is string str)
            {
                href = str;
            }

            var (isBool, val) = TryBool(parameters[1]);
            if (isBool)
            {
                clickTrackingOff = val;
            }

            return (href, clickTrackingOff);
        }

        private static (bool isBool, bool result) TryBool(object parameter)
        {
            if (parameter is bool boolean)
            {
                return (true, boolean);
            }
            else if (parameter is string str && (str == "true" || str == "false"))
            {
                return (true, str == "true");
            }
            return (false, false);
        }
    }
}
