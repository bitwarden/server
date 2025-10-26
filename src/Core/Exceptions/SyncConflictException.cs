using System;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Exceptions;

public class SyncConflictException : ConflictException
{
    public SyncConflictException(Cipher serverCipher)
        : base("The cipher you are updating is out of date. Please save your work, sync your vault, and try again.")
    {
        ServerCipher = serverCipher ?? throw new ArgumentNullException(nameof(serverCipher));
    }

    public Cipher ServerCipher { get; }
}
