namespace Bit.Core.Enums
{
    public enum PushType : byte
    {
        SyncCipherUpdate = 0,
        SyncCipherCreate = 1,
        SyncLoginDelete = 2,
        SyncFolderDelete = 3,
        SyncCiphers = 4
    }
}
