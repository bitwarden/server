namespace Bit.Api.Models.Response.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class RevokeSponsorshipResponseModel
    {
        public string CloudCancelToken { get; set; }

        public RevokeSponsorshipResponseModel(string cloudCancelToken)
        {
            CloudCancelToken = cloudCancelToken;
        }
    }
}
