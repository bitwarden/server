namespace Bit.Core.Enums
{
    public enum EventType : int
    {
        User_LoggedIn = 1000,
        User_ChangedPassword = 1001,
        User_Enabled2fa = 1002,
        User_Disabled2fa = 1003,
        User_Recovered2fa = 1004,
        User_FailedLogIn = 1005,
        User_FailedLogIn2fa = 1006,

        Cipher_Created = 2000,
        Cipher_Edited = 2001,
        Cipher_Deleted = 2002
    }
}
