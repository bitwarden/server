using System;

namespace Bit.Core.Tokens
{
    public class BillingSyncTokenable : Tokenable
    {
        public string BillingSyncKey { get; set; }
        public Guid OrganizationId { get; set; }
        public override bool Valid => OrganizationId != Guid.Empty && !string.IsNullOrWhiteSpace(BillingSyncKey);
    }
}
