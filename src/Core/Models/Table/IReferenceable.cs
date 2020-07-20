using System;

namespace Bit.Core.Models
{
    public interface IReferenceable
    {
        Guid Id { get; set; }
        string ReferenceData { get; set; }
        bool IsUser();
    }
}
