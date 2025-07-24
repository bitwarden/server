#nullable enable

using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class KdfSettings
{
    public required KdfType KdfType { get; init; }
    public required int Iterations { get; init; }
    public int? Memory { get; init; }
    public int? Parallelism { get; init; }

    public void ValidateUnchangedForUser(User user)
    {
        if (user.Kdf != KdfType || user.KdfIterations != Iterations || user.KdfMemory != Memory || user.KdfParallelism != Parallelism)
        {
            throw new ArgumentException("Invalid KDF settings.");
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is not KdfSettings other)
        {
            return false;
        }

        return KdfType == other.KdfType &&
               Iterations == other.Iterations &&
               Memory == other.Memory &&
               Parallelism == other.Parallelism;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(KdfType, Iterations, Memory, Parallelism);
    }
}
