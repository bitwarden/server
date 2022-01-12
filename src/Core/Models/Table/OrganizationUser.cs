using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class OrganizationUser : ITableObject<Guid>, IExternal
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        [MaxLength(256)]
        public string Email { get; set; }
        public string Key { get; set; }
        public string ResetPasswordKey { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public OrganizationUserType Type { get; set; }
        public bool AccessAll { get; set; }
        [MaxLength(300)]
        public string ExternalId { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
        public string Permissions { get; set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }

        public OrganizationUser AcceptUser(Guid userId)
        {
            Status = OrganizationUserStatusType.Accepted;
            UserId = userId;
            Email = null;
            return this;
        }

        public OrganizationUser ConfirmUser(string key)
        {
            Status = OrganizationUserStatusType.Confirmed;
            Key = key;
            Email = null;
            return this;
        }
    }
}
