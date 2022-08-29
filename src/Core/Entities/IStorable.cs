namespace Bit.Core.Entities;

public interface IStorable
{
    long? Storage { get; set; }
    short? MaxStorageGb { get; set; }
    long StorageBytesRemaining();
    long StorageBytesRemaining(short maxStorageGb);
}
