/*
Original source modified from https://github.com/duosecurity/duo_dotnet

=============================================================================
=============================================================================

ref: https://github.com/duosecurity/duo_dotnet/blob/master/LICENSE

Copyright (c) 2011, Duo Security, Inc.
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

using System.Security.Cryptography;
using System.Text;

namespace Bit.Core.Utilities.Duo;

public static class DuoWeb
{
    private const string DuoProfix = "TX";
    private const string AppPrefix = "APP";
    private const string AuthPrefix = "AUTH";
    private const int DuoExpire = 300;
    private const int AppExpire = 3600;
    private const int IKeyLength = 20;
    private const int SKeyLength = 40;
    private const int AKeyLength = 40;

    public static string ErrorUser = "ERR|The username passed to sign_request() is invalid.";
    public static string ErrorIKey = "ERR|The Duo integration key passed to sign_request() is invalid.";
    public static string ErrorSKey = "ERR|The Duo secret key passed to sign_request() is invalid.";
    public static string ErrorAKey = "ERR|The application secret key passed to sign_request() must be at least " +
        "40 characters.";
    public static string ErrorUnknown = "ERR|An unknown error has occurred.";

    // throw on invalid bytes
    private static Encoding _encoding = new UTF8Encoding(false, true);
    private static DateTime _epoc = new DateTime(1970, 1, 1);

    /// <summary>
    /// Generate a signed request for Duo authentication.
    /// The returned value should be passed into the Duo.init() call
    /// in the rendered web page used for Duo authentication.
    /// </summary>
    /// <param name="ikey">Duo integration key</param>
    /// <param name="skey">Duo secret key</param>
    /// <param name="akey">Application secret key</param>
    /// <param name="username">Primary-authenticated username</param>
    /// <param name="currentTime">(optional) The current UTC time</param>
    /// <returns>signed request</returns>
    public static string SignRequest(string ikey, string skey, string akey, string username,
        DateTime? currentTime = null)
    {
        string duoSig;
        string appSig;

        var currentTimeValue = currentTime ?? DateTime.UtcNow;

        if (username == string.Empty)
        {
            return ErrorUser;
        }
        if (username.Contains("|"))
        {
            return ErrorUser;
        }
        if (ikey.Length != IKeyLength)
        {
            return ErrorIKey;
        }
        if (skey.Length != SKeyLength)
        {
            return ErrorSKey;
        }
        if (akey.Length < AKeyLength)
        {
            return ErrorAKey;
        }

        try
        {
            duoSig = SignVals(skey, username, ikey, DuoProfix, DuoExpire, currentTimeValue);
            appSig = SignVals(akey, username, ikey, AppPrefix, AppExpire, currentTimeValue);
        }
        catch
        {
            return ErrorUnknown;
        }

        return $"{duoSig}:{appSig}";
    }

    /// <summary>
    /// Validate the signed response returned from Duo.
    /// Returns the username of the authenticated user, or null.
    /// </summary>
    /// <param name="ikey">Duo integration key</param>
    /// <param name="skey">Duo secret key</param>
    /// <param name="akey">Application secret key</param>
    /// <param name="sigResponse">The signed response POST'ed to the server</param>
    /// <param name="currentTime">(optional) The current UTC time</param>
    /// <returns>authenticated username, or null</returns>
    public static string VerifyResponse(string ikey, string skey, string akey, string sigResponse,
        DateTime? currentTime = null)
    {
        string authUser = null;
        string appUser = null;
        var currentTimeValue = currentTime ?? DateTime.UtcNow;

        try
        {
            var sigs = sigResponse.Split(':');
            var authSig = sigs[0];
            var appSig = sigs[1];

            authUser = ParseVals(skey, authSig, AuthPrefix, ikey, currentTimeValue);
            appUser = ParseVals(akey, appSig, AppPrefix, ikey, currentTimeValue);
        }
        catch
        {
            return null;
        }

        if (authUser != appUser)
        {
            return null;
        }

        return authUser;
    }

    private static string SignVals(string key, string username, string ikey, string prefix, long expire,
        DateTime currentTime)
    {
        var ts = (long)(currentTime - _epoc).TotalSeconds;
        expire = ts + expire;
        var val = $"{username}|{ikey}|{expire.ToString()}";
        var cookie = $"{prefix}|{Encode64(val)}";
        var sig = Sign(key, cookie);
        return $"{cookie}|{sig}";
    }

    private static string ParseVals(string key, string val, string prefix, string ikey, DateTime currentTime)
    {
        var ts = (long)(currentTime - _epoc).TotalSeconds;

        var parts = val.Split('|');
        if (parts.Length != 3)
        {
            return null;
        }

        var uPrefix = parts[0];
        var uB64 = parts[1];
        var uSig = parts[2];

        var sig = Sign(key, $"{uPrefix}|{uB64}");
        if (Sign(key, sig) != Sign(key, uSig))
        {
            return null;
        }

        if (uPrefix != prefix)
        {
            return null;
        }

        var cookie = Decode64(uB64);
        var cookieParts = cookie.Split('|');
        if (cookieParts.Length != 3)
        {
            return null;
        }

        var username = cookieParts[0];
        var uIKey = cookieParts[1];
        var expire = cookieParts[2];

        if (uIKey != ikey)
        {
            return null;
        }

        var expireTs = Convert.ToInt32(expire);
        if (ts >= expireTs)
        {
            return null;
        }

        return username;
    }

    private static string Sign(string skey, string data)
    {
        var keyBytes = Encoding.ASCII.GetBytes(skey);
        var dataBytes = Encoding.ASCII.GetBytes(data);

        using (var hmac = new HMACSHA1(keyBytes))
        {
            var hash = hmac.ComputeHash(dataBytes);
            var hex = BitConverter.ToString(hash);
            return hex.Replace("-", "").ToLower();
        }
    }

    private static string Encode64(string plaintext)
    {
        var plaintextBytes = _encoding.GetBytes(plaintext);
        return Convert.ToBase64String(plaintextBytes);
    }

    private static string Decode64(string encoded)
    {
        var plaintextBytes = Convert.FromBase64String(encoded);
        return _encoding.GetString(plaintextBytes);
    }
}
