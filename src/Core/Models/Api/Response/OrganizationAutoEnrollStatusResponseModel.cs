using System;

namespace Bit.Core.Models.Api.Response
{
    public class OrganizationAutoEnrollStatusResponseModel : ResponseModel
    {
        public OrganizationAutoEnrollStatusResponseModel(Guid orgId, bool resetPasswordEnabled) : base("organizationAutoEnrollStatus")
        {
            Id = orgId.ToString();
            ResetPasswordEnabled = resetPasswordEnabled;
        }
        
        public string Id { get; set; }
        public bool ResetPasswordEnabled { get; set; }
    }
}
