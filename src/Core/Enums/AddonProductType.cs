namespace Bit.Core.Enums;

/// <summary>
/// Used to identify the various types of "addon" products that can be added
/// to an existing product subscription.
/// </summary>
public enum AddonProductType : byte
{
    PasswordManager_Storage = 0,
    SecretsManager_ServiceAccounts = 1
}
