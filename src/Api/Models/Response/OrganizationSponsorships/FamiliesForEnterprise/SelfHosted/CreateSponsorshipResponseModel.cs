namespace Bit.Api.Models.Response.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class CreateSponsorshipResponseModel
    {
        public string CloudCreateToken { get; set; }

        public CreateSponsorshipResponseModel(string cloudCreateToken)
        {
            CloudCreateToken = cloudCreateToken;
        }
    }
}
