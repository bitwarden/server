using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class ProfileOrganizationResponseModel : ResponseModel
    {
        public ProfileOrganizationResponseModel(OrganizationUserOrganizationDetails organization)
            : base("profileOrganization")
        {
            Id = organization.OrganizationId.ToString();
            Name = organization.Name;
            UseGroups = organization.UseGroups;
            UseDirectory = organization.UseDirectory;
            UseEvents = organization.UseEvents;
            UseTotp = organization.UseTotp;
            Use2fa = organization.Use2fa;
            UseApi = organization.UseApi;
            UsersGetPremium = organization.UsersGetPremium;
            SelfHost = organization.SelfHost;
            Seats = organization.Seats;
            MaxCollections = organization.MaxCollections;
            MaxStorageGb = organization.MaxStorageGb;
            Key = organization.Key;
            Status = organization.Status;
            Type = organization.Type;
            Enabled = organization.Enabled;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseEvents { get; set; }
        public bool UseTotp { get; set; }
        public bool Use2fa { get; set; }
        public bool UseApi { get; set; }
        public bool UsersGetPremium { get; set; }
        public bool SelfHost { get; set; }
        public int Seats { get; set; }
        public int MaxCollections { get; set; }
        public short? MaxStorageGb { get; set; }
        public string Key { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public OrganizationUserType Type { get; set; }
        public bool Enabled { get; set; }
    }
}
