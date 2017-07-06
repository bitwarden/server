using System;

namespace Bit.Core.Models.Table
{
    public interface IRevisable
    {
        DateTime CreationDate { get; }
        DateTime RevisionDate { get; }
    }
}
