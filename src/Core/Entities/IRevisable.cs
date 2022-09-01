namespace Bit.Core.Entities;

public interface IRevisable
{
    DateTime CreationDate { get; }
    DateTime RevisionDate { get; }
}
