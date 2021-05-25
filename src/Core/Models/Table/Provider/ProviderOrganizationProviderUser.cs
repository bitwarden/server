using System;
using Bit.Core.Enums.Provider;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table.Provider
{
    public class ProviderOrganizationProviderUser : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid ProviderOrganizationId { get; set; }
        public Guid ProviderUserId { get; set; }
        public ProviderOrganizationProviderUserType Type { get; set; }
        public string Permissions { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            if (Id == default)
            {
                Id = CoreHelpers.GenerateComb();
            }
        }
    }
}
