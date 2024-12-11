namespace Bit.Core.Entities;

#nullable enable

public interface ITableObject<T>
    where T : IEquatable<T>
{
    T Id { get; set; }
    void SetNewId();
}
