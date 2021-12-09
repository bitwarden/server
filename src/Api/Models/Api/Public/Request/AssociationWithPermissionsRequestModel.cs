using Bit.Core.Models.Data;

namespace Bit.Web.Models.Api.Public
{
    public class AssociationWithPermissionsRequestModel : AssociationWithPermissionsBaseModel
    {
        public SelectionReadOnly ToSelectionReadOnly()
        {
            return new SelectionReadOnly
            {
                Id = Id.Value,
                ReadOnly = ReadOnly.Value
            };
        }
    }
}
