using System;

namespace Bit.Core.Models
{
    public interface IReferenceable
    {
        Guid Id { get; set; }
        string ReferenceId { get; set; }
        bool IsUser();
    }
}
