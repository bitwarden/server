namespace Bit.Core.Enums
{
    public enum EventType : int
    {
        User_LoggedIn = 1000,
        User_ChangedPassword = 1001,
        User_Updated2fa = 1002,
        User_Disabled2fa = 1003,
        User_Recovered2fa = 1004,
        User_FailedLogIn = 1005,
        User_FailedLogIn2fa = 1006,

        Cipher_Created = 1100,
        Cipher_Updated = 1101,
        Cipher_Deleted = 1102,
        Cipher_AttachmentCreated = 1103,
        Cipher_AttachmentDeleted = 1104,
        Cipher_Shared = 1105,
        Cipher_UpdatedCollections = 1106,

        Collection_Created = 1300,
        Collection_Updated = 1301,
        Collection_Deleted = 1302,

        Group_Created = 1400,
        Group_Updated = 1401,
        Group_Deleted = 1402,

        OrganizationUser_Invited = 1500,
        OrganizationUser_Confirmed = 1501,
        OrganizationUser_Updated = 1502,
        OrganizationUser_Removed = 1503,
        OrganizationUser_UpdatedGroups = 1504,

        Organization_Updated = 1600,
        Organization_PurgedVault = 1601,
    }
}
