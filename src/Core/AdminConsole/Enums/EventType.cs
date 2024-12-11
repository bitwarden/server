namespace Bit.Core.Enums;

// Increment by 100 for each new set of events
public enum EventType : int
{
    User_LoggedIn = 1000,
    User_ChangedPassword = 1001,
    User_Updated2fa = 1002,
    User_Disabled2fa = 1003,
    User_Recovered2fa = 1004,
    User_FailedLogIn = 1005,
    User_FailedLogIn2fa = 1006,
    User_ClientExportedVault = 1007,
    User_UpdatedTempPassword = 1008,
    User_MigratedKeyToKeyConnector = 1009,
    User_RequestedDeviceApproval = 1010,
    User_TdeOffboardingPasswordSet = 1011,

    Cipher_Created = 1100,
    Cipher_Updated = 1101,
    Cipher_Deleted = 1102,
    Cipher_AttachmentCreated = 1103,
    Cipher_AttachmentDeleted = 1104,
    Cipher_Shared = 1105,
    Cipher_UpdatedCollections = 1106,
    Cipher_ClientViewed = 1107,
    Cipher_ClientToggledPasswordVisible = 1108,
    Cipher_ClientToggledHiddenFieldVisible = 1109,
    Cipher_ClientToggledCardCodeVisible = 1110,
    Cipher_ClientCopiedPassword = 1111,
    Cipher_ClientCopiedHiddenField = 1112,
    Cipher_ClientCopiedCardCode = 1113,
    Cipher_ClientAutofilled = 1114,
    Cipher_SoftDeleted = 1115,
    Cipher_Restored = 1116,
    Cipher_ClientToggledCardNumberVisible = 1117,

    Collection_Created = 1300,
    Collection_Updated = 1301,
    Collection_Deleted = 1302,

    Group_Created = 1400,
    Group_Updated = 1401,
    Group_Deleted = 1402,

    OrganizationUser_Invited = 1500,
    OrganizationUser_Confirmed = 1501,
    OrganizationUser_Updated = 1502,
    OrganizationUser_Removed = 1503, // Organization user data was deleted
    OrganizationUser_UpdatedGroups = 1504,
    OrganizationUser_UnlinkedSso = 1505,
    OrganizationUser_ResetPassword_Enroll = 1506,
    OrganizationUser_ResetPassword_Withdraw = 1507,
    OrganizationUser_AdminResetPassword = 1508,
    OrganizationUser_ResetSsoLink = 1509,
    OrganizationUser_FirstSsoLogin = 1510,
    OrganizationUser_Revoked = 1511,
    OrganizationUser_Restored = 1512,
    OrganizationUser_ApprovedAuthRequest = 1513,
    OrganizationUser_RejectedAuthRequest = 1514,
    OrganizationUser_Deleted = 1515, // Both user and organization user data were deleted
    OrganizationUser_Left = 1516, // User voluntarily left the organization

    Organization_Updated = 1600,
    Organization_PurgedVault = 1601,
    Organization_ClientExportedVault = 1602,
    Organization_VaultAccessed = 1603,
    Organization_EnabledSso = 1604,
    Organization_DisabledSso = 1605,
    Organization_EnabledKeyConnector = 1606,
    Organization_DisabledKeyConnector = 1607,
    Organization_SponsorshipsSynced = 1608,
    Organization_CollectionManagement_Updated = 1609,

    Policy_Updated = 1700,

    ProviderUser_Invited = 1800,
    ProviderUser_Confirmed = 1801,
    ProviderUser_Updated = 1802,
    ProviderUser_Removed = 1803,

    ProviderOrganization_Created = 1900,
    ProviderOrganization_Added = 1901,
    ProviderOrganization_Removed = 1902,
    ProviderOrganization_VaultAccessed = 1903,

    OrganizationDomain_Added = 2000,
    OrganizationDomain_Removed = 2001,
    OrganizationDomain_Verified = 2002,
    OrganizationDomain_NotVerified = 2003,

    Secret_Retrieved = 2100,
}
