using System;

namespace Bit.Core
{
    public interface IDataObject<T> where T : IEquatable<T>
    {
        T Id { get; set; }
        void SetNewId();
    }
}
