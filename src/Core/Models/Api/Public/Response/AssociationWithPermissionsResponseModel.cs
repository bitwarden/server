using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api.Public
{
    public class AssociationWithPermissionsResponseModel : AssociationWithPermissionsBaseModel
    {
        public AssociationWithPermissionsResponseModel(SelectionReadOnly selection)
        {
            if(selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }
            Id = selection.Id;
            ReadOnly = selection.ReadOnly;
        }
    }
}
