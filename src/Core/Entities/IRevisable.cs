namespace Bit.Core.Entities;

#nullable enable

public interface IRevisable
{
    DateTime CreationDate { get; }
    DateTime RevisionDate { get; }
}
