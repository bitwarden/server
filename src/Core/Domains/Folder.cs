using Bit.Core.Enums;

namespace Bit.Core.Domains
{
    public class Folder : Cipher, IDataObject
    {
        public override CipherType CipherType { get; protected set; } = CipherType.Folder;
    }
}
