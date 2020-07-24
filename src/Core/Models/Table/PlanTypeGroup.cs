namespace Bit.Core.Models.Table
{
    public class PlanTypeGroup: ITableObject<int>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool CanBeUsedByBusiness { get; set; }
        public int BaseSeats { get; set; }
        public int BaseStorageGb { get; set; }
        public int? MaxCollections { get; set; }
        public bool HasAdditionalSeatsOption { get; set; }
        public bool HasAdditionalStorageOption { get; set; }
        public bool HasPremiumAccessOption { get; set; }
        public int TrialPeriodDays { get; set; }
        public bool HasSelfHost { get; set; }
        public bool HasPolicies { get; set; }
        public bool HasGroups { get; set; }
        public bool HasDirectory { get; set; }
        public bool HasEvents { get; set; }
        public bool HasTotp { get; set; }
        public bool Has2fa { get; set; }
        public bool HasApi { get; set; }
        public bool UsersGetPremium { get; set; }
        public bool HasSso { get; set; }
        public int SortOrder { get; set; }
        public bool IsLegacy { get; set; }


        public void SetNewId()
        {
            // do nothing because it is an identity
        }
    }
}
