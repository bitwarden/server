/*
Original source modified from https://github.com/duosecurity/duo_api_csharp

=============================================================================
=============================================================================

Copyright (c) 2018 Duo Security
All rights reserved
*/

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Bit.Core.Utilities.Duo;

public class DuoApi
{
    private const string UrlScheme = "https";
    private const string UserAgent = "Bitwarden_DuoAPICSharp/1.0 (.NET Core)";

    private readonly string _host;
    private readonly string _ikey;
    private readonly string _skey;

    public DuoApi(string ikey, string skey, string host)
    {
        _ikey = ikey;
        _skey = skey;
        _host = host;

        if (!ValidHost(host))
        {
            throw new DuoException("Invalid Duo host configured.", new ArgumentException(nameof(host)));
        }
    }

    public static bool ValidHost(string host)
    {
        if (Uri.TryCreate($"https://{host}", UriKind.Absolute, out var uri))
        {
            return (string.IsNullOrWhiteSpace(uri.PathAndQuery) || uri.PathAndQuery == "/") &&
                uri.Host.StartsWith("api-") &&
                (uri.Host.EndsWith(".duosecurity.com") || uri.Host.EndsWith(".duofederal.com"));
        }
        return false;
    }

    public static string CanonicalizeParams(Dictionary<string, string> parameters)
    {
        var ret = new List<string>();
        foreach (var pair in parameters)
        {
            var p = string.Format("{0}={1}", HttpUtility.UrlEncode(pair.Key), HttpUtility.UrlEncode(pair.Value));
            // Signatures require upper-case hex digits.
            p = Regex.Replace(p, "(%[0-9A-Fa-f][0-9A-Fa-f])", c => c.Value.ToUpperInvariant());
            // Escape only the expected characters.
            p = Regex.Replace(p, "([!'()*])", c => "%" + Convert.ToByte(c.Value[0]).ToString("X"));
            p = p.Replace("%7E", "~");
            // UrlEncode converts space (" ") to "+". The
            // signature algorithm requires "%20" instead. Actual
            // + has already been replaced with %2B.
            p = p.Replace("+", "%20");
            ret.Add(p);
        }

        ret.Sort(StringComparer.Ordinal);
        return string.Join("&", ret.ToArray());
    }

    protected string CanonicalizeRequest(string method, string path, string canonParams, string date)
    {
        string[] lines = {
            date,
            method.ToUpperInvariant(),
            _host.ToLower(),
            path,
            canonParams,
        };
        return string.Join("\n", lines);
    }

    public string Sign(string method, string path, string canonParams, string date)
    {
        var canon = CanonicalizeRequest(method, path, canonParams, date);
        var sig = HmacSign(canon);
        var auth = string.Concat(_ikey, ':', sig);
        return string.Concat("Basic ", Encode64(auth));
    }

    public string ApiCall(string method, string path, Dictionary<string, string> parameters = null)
    {
        return ApiCall(method, path, parameters, 0, out var statusCode);
    }

    /// <param name="timeout">The request timeout, in milliseconds.
    /// Specify 0 to use the system-default timeout. Use caution if
    /// you choose to specify a custom timeout - some API
    /// calls (particularly in the Auth APIs) will not
    /// return a response until an out-of-band authentication process
    /// has completed. In some cases, this may take as much as a
    /// small number of minutes.</param>
    public string ApiCall(string method, string path, Dictionary<string, string> parameters, int timeout,
        out HttpStatusCode statusCode)
    {
        if (parameters == null)
        {
            parameters = new Dictionary<string, string>();
        }

        var canonParams = CanonicalizeParams(parameters);
        var query = string.Empty;
        if (!method.Equals("POST") && !method.Equals("PUT"))
        {
            if (parameters.Count > 0)
            {
                query = "?" + canonParams;
            }
        }
        var url = string.Format("{0}://{1}{2}{3}", UrlScheme, _host, path, query);

        var dateString = RFC822UtcNow();
        var auth = Sign(method, path, canonParams, dateString);

        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = method;
        request.Accept = "application/json";
        request.Headers.Add("Authorization", auth);
        request.Headers.Add("X-Duo-Date", dateString);
        request.UserAgent = UserAgent;

        if (method.Equals("POST") || method.Equals("PUT"))
        {
            var data = Encoding.UTF8.GetBytes(canonParams);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(data, 0, data.Length);
            }
        }
        if (timeout > 0)
        {
            request.Timeout = timeout;
        }

        // Do the request and process the result.
        HttpWebResponse response;
        try
        {
            response = (HttpWebResponse)request.GetResponse();
        }
        catch (WebException ex)
        {
            response = (HttpWebResponse)ex.Response;
            if (response == null)
            {
                throw;
            }
        }
        using (var reader = new StreamReader(response.GetResponseStream()))
        {
            statusCode = response.StatusCode;
            return reader.ReadToEnd();
        }
    }

    public T JSONApiCall<T>(string method, string path, Dictionary<string, string> parameters = null)
        where T : class
    {
        return JSONApiCall<T>(method, path, parameters, 0);
    }

    /// <param name="timeout">The request timeout, in milliseconds.
    /// Specify 0 to use the system-default timeout. Use caution if
    /// you choose to specify a custom timeout - some API
    /// calls (particularly in the Auth APIs) will not
    /// return a response until an out-of-band authentication process
    /// has completed. In some cases, this may take as much as a
    /// small number of minutes.</param>
    public T JSONApiCall<T>(string method, string path, Dictionary<string, string> parameters, int timeout)
        where T : class
    {
        var res = ApiCall(method, path, parameters, timeout, out var statusCode);
        try
        {
            // TODO: We should deserialize this into our own DTO and not work on dictionaries.
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(res);
            if (dict["stat"].ToString() == "OK")
            {
                return JsonSerializer.Deserialize<T>(dict["response"].ToString());
            }

            var check = ToNullableInt(dict["code"].ToString());
            var code = check.GetValueOrDefault(0);
            var messageDetail = string.Empty;
            if (dict.ContainsKey("message_detail"))
            {
                messageDetail = dict["message_detail"].ToString();
            }
            throw new ApiException(code, (int)statusCode, dict["message"].ToString(), messageDetail);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new BadResponseException((int)statusCode, e);
        }
    }

    private int? ToNullableInt(string s)
    {
        int i;
        if (int.TryParse(s, out i))
        {
            return i;
        }
        return null;
    }

    private string HmacSign(string data)
    {
        var keyBytes = Encoding.ASCII.GetBytes(_skey);
        var dataBytes = Encoding.ASCII.GetBytes(data);

        using (var hmac = new HMACSHA1(keyBytes))
        {
            var hash = hmac.ComputeHash(dataBytes);
            var hex = BitConverter.ToString(hash);
            return hex.Replace("-", string.Empty).ToLower();
        }
    }

    private static string Encode64(string plaintext)
    {
        var plaintextBytes = Encoding.ASCII.GetBytes(plaintext);
        return Convert.ToBase64String(plaintextBytes);
    }

    private static string RFC822UtcNow()
    {
        // Can't use the "zzzz" format because it adds a ":"
        // between the offset's hours and minutes.
        var dateString = DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var offset = 0;
        var zone = "+" + offset.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        dateString += " " + zone.PadRight(5, '0');
        return dateString;
    }
}

public class DuoException : Exception
{
    public int HttpStatus { get; private set; }

    public DuoException(string message, Exception inner)
        : base(message, inner)
    { }

    public DuoException(int httpStatus, string message, Exception inner)
        : base(message, inner)
    {
        HttpStatus = httpStatus;
    }
}

public class ApiException : DuoException
{
    public int Code { get; private set; }
    public string ApiMessage { get; private set; }
    public string ApiMessageDetail { get; private set; }

    public ApiException(int code, int httpStatus, string apiMessage, string apiMessageDetail)
        : base(httpStatus, FormatMessage(code, apiMessage, apiMessageDetail), null)
    {
        Code = code;
        ApiMessage = apiMessage;
        ApiMessageDetail = apiMessageDetail;
    }

    private static string FormatMessage(int code, string apiMessage, string apiMessageDetail)
    {
        return string.Format("Duo API Error {0}: '{1}' ('{2}')", code, apiMessage, apiMessageDetail);
    }
}

public class BadResponseException : DuoException
{
    public BadResponseException(int httpStatus, Exception inner)
        : base(httpStatus, FormatMessage(httpStatus, inner), inner)
    { }

    private static string FormatMessage(int httpStatus, Exception inner)
    {
        var innerMessage = "(null)";
        if (inner != null)
        {
            innerMessage = string.Format("'{0}'", inner.Message);
        }
        return string.Format("Got error {0} with HTTP Status {1}", innerMessage, httpStatus);
    }
}
