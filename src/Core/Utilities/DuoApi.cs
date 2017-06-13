/*
Original source modified from https://github.com/duosecurity/duo_api_csharp

=============================================================================
=============================================================================

ref: https://github.com/duosecurity/duo_api_csharp/blob/master/LICENSE

Copyright (c) 2013, Duo Security, Inc.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

1. Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
    notice, this list of conditions and the following disclaimer in the
    documentation and/or other materials provided with the distribution.
3. The name of the author may not be used to endorse or promote products
    derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Bit.Core.Utilities.Duo
{
    public class DuoApi
    {
        public const string DefaultAgent = "Duo.NET, bitwarden";

        private readonly string _ikey;
        private readonly string _skey;
        private readonly string _host;
        private readonly string _userAgent;

        public DuoApi(string ikey, string skey, string host)
            : this(ikey, skey, host, null)
        { }

        protected DuoApi(string ikey, string skey, string host, string userAgent)
        {
            _ikey = ikey;
            _skey = skey;
            _host = host;
            _userAgent = string.IsNullOrWhiteSpace(userAgent) ? DefaultAgent : userAgent;
        }

        public async Task<Tuple<string, HttpStatusCode>> ApiCallAsync(HttpMethod method, string path,
            Dictionary<string, string> parameters, int? timeout = null, DateTime? date = null)
        {
            var canonParams = CanonicalizeParams(parameters);
            var query = string.Empty;
            if(method != HttpMethod.Post && method != HttpMethod.Put && parameters.Count > 0)
            {
                query = "?" + canonParams;
            }

            var url = $"https://{_host}{path}{query}";

            var dateString = DateToRFC822(date.GetValueOrDefault(DateTime.UtcNow));
            var auth = Sign(method.ToString(), path, canonParams, dateString);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Authorization", auth);
            client.DefaultRequestHeaders.Add("X-Duo-Date", dateString);
            client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

            if(timeout.GetValueOrDefault(0) > 0)
            {
                client.Timeout = new TimeSpan(0, 0, 0, 0, timeout.Value);
            }

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = method
            };

            if(method == HttpMethod.Post || method == HttpMethod.Put)
            {
                request.Content = new FormUrlEncodedContent(parameters);
            }

            HttpResponseMessage response = null;
            try
            {
                response = await client.SendAsync(request);
            }
            catch(WebException)
            {
                if(response?.Content == null)
                {
                    throw;
                }
            }

            var result = await response.Content.ReadAsStringAsync();
            return new Tuple<string, HttpStatusCode>(result, response.StatusCode);
        }

        public async Task<T> JSONApiCallAsync<T>(HttpMethod method, string path, Dictionary<string, string> parameters,
            int? timeout = null, DateTime? date = null) where T : class
        {
            var resTuple = await ApiCallAsync(method, path, parameters, timeout, date);
            var res = resTuple.Item1;
            HttpStatusCode statusCode = resTuple.Item2;

            try
            {
                var resDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(res);
                var stat = resDict["stat"] as string;
                if(stat == "OK")
                {
                    return JsonConvert.DeserializeObject<T>(resDict["response"].ToString());
                }
                else
                {
                    var code = resDict["code"] as int?;
                    var message = resDict["message"] as string;

                    var messageDetail = string.Empty;
                    if(resDict.ContainsKey("message_detail"))
                    {
                        messageDetail = resDict["message_detail"] as string;
                    }

                    throw new DuoApiException(code.GetValueOrDefault(0), statusCode, message, messageDetail);
                }
            }
            catch(Exception e)
            {
                throw new DuoBadResponseException(statusCode, e);
            }
        }

        private string CanonicalizeParams(Dictionary<string, string> parameters)
        {
            var ret = new List<string>();
            foreach(var pair in parameters)
            {
                var p = $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}";
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

        private string CanonicalizeRequest(string method, string path, string canon_params, string date)
        {
            string[] lines = { date, method.ToUpperInvariant(), _host.ToLower(), path, canon_params };
            return string.Join("\n", lines);
        }

        private string Sign(string method, string path, string canon_params, string date)
        {
            var canon = CanonicalizeRequest(method, path, canon_params, date);
            var sig = HmacSign(canon);
            var auth = $"{_ikey }:{sig}";
            var authBytes = Encoding.ASCII.GetBytes(auth);
            return $"Basic {Convert.ToBase64String(authBytes)}";
        }

        private string HmacSign(string data)
        {
            var keyBytes = Encoding.ASCII.GetBytes(_skey);
            var dataBytes = Encoding.ASCII.GetBytes(data);

            using(var hmac = new HMACSHA1(keyBytes))
            {
                var hash = hmac.ComputeHash(dataBytes);
                var hex = BitConverter.ToString(hash);
                return hex.Replace("-", "").ToLower();
            }
        }

        private string DateToRFC822(DateTime date)
        {
            // Can't use the "zzzz" format because it adds a ":"
            // between the offset's hours and minutes.
            var dateString = date.ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture);

            // TODO: Get proper timezone offset. hardcoded to UTC for now.
            var offset = 0;

            string zone;
            // + or -, then 0-pad, then offset, then more 0-padding.
            if(offset < 0)
            {
                offset *= -1;
                zone = "-";
            }
            else
            {
                zone = "+";
            }

            zone += offset.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
            dateString += (" " + zone.PadRight(5, '0'));
            return dateString;
        }
    }

    public class DuoException : Exception
    {
        public HttpStatusCode Status { get; private set; }

        public DuoException(HttpStatusCode status, string message, Exception inner)
            : base(message, inner)
        {
            Status = status;
        }
    }

    public class DuoApiException : DuoException
    {
        public int Code { get; private set; }
        public string ApiMessage { get; private set; }
        public string ApiMessageDetail { get; private set; }

        public DuoApiException(int code, HttpStatusCode status, string message, string messageDetail)
            : base(status, FormatMessage(code, message, messageDetail), null)
        {
            Code = code;
            ApiMessage = message;
            ApiMessageDetail = messageDetail;
        }

        private static string FormatMessage(int code, string message, string messageDetail)
        {
            return $"Duo API Error {code}: '{message}' ('{messageDetail}').";
        }
    }

    public class DuoBadResponseException : DuoException
    {
        public DuoBadResponseException(HttpStatusCode status, Exception inner)
            : base(status, FormatMessage(status, inner), inner)
        { }

        private static string FormatMessage(HttpStatusCode status, Exception inner)
        {
            var innerMessage = "(null)";
            if(inner != null)
            {
                innerMessage = string.Format("'{0}'", inner.Message);
            }

            return $"Got error '{innerMessage}' with HTTP status {(int)status}.";
        }
    }
}
