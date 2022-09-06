namespace Bit.Scim.Commands.Groups.Interfaces
{
    public interface IDeleteGroupCommand
    {
        Task DeleteGroupAsync(Guid organizationId, Guid id);
    }
}
