using System;

namespace Bit.Core.Models.Api.Response
{
    public class OrganizationAutoEnrollStatusResponseModel : ResponseModel
    {
        public OrganizationAutoEnrollStatusResponseModel(Guid orgId, bool autoEnrollEnabled) : base("organizationAutoEnrollStatus")
        {
            Id = orgId.ToString();
            AutoEnrollEnabled = autoEnrollEnabled;
        }
        
        public string Id { get; set; }
        public bool AutoEnrollEnabled { get; set; }
    }
}
