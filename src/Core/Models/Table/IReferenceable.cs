namespace Bit.Core.Models
{
    public interface IReferenceable
    {
        string Id { get; set; }
        string ReferenceId { get; set; }
        bool IsUser();
    }
}
