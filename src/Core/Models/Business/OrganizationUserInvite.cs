using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Business
{
    public class OrganizationUserInvite
    {
        public IEnumerable<string> Emails { get; set; }
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAll { get; set; }
        public Permissions Permissions { get; set; }
        public IEnumerable<SelectionReadOnly> Collections { get; set; }

        public OrganizationUserInvite() {}

        public OrganizationUserInvite(OrganizationUserInviteRequestModel requestModel) 
        {
            Emails = requestModel.Emails;
            Type = requestModel.Type.Value;
            AccessAll = requestModel.AccessAll;
            Permissions = requestModel.Permissions;

            if (requestModel.Collections != null && requestModel.Collections.Any())
            {
                Collections = requestModel.Collections.Select(c => c.ToSelectionReadOnly());
            }
        }
    }
}
