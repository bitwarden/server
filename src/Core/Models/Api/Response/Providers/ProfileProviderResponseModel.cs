using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class ProfileProviderResponseModel : ResponseModel
    {
        public ProfileProviderResponseModel(ProviderUserProviderDetails provider)
            : base("profileProvider")
        {
            Id = provider.ProviderId.ToString();
            Name = provider.Name;
            Key = provider.Key;
            Status = provider.Status;
            Type = provider.Type;
            Enabled = provider.Enabled;
            Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(provider.Permissions);
            UserId = provider.UserId?.ToString();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public ProviderUserStatusType Status { get; set; }
        public ProviderUserType Type { get; set; }
        public bool Enabled { get; set; }
        public Permissions Permissions { get; set; }
        public string UserId { get; set; }
    }
}
