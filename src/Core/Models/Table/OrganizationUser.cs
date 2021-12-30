using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class OrganizationUser : ITableObject<Guid>, IExternal, ICloneable
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
            var orgUser = Clone();
            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.UserId = userId;
            orgUser.Email = null;
            return orgUser;
        }

        public OrganizationUser ConfirmUser(string key)
        {
            var orgUser = Clone();
            orgUser.Status = OrganizationUserStatusType.Confirmed;
            orgUser.Key = key;
            orgUser.Email = null;
            return orgUser;
        }

        object ICloneable.Clone() => Clone();
        public OrganizationUser Clone()
        {
            return new OrganizationUser
            {
                Id = Id,
                OrganizationId = OrganizationId,
                UserId = UserId,
                Email = Email,
                Key = Key,
                ResetPasswordKey = ResetPasswordKey,
                Status = Status,
                Type = Type,
                AccessAll = AccessAll,
                ExternalId = ExternalId,
                CreationDate = CreationDate,
                RevisionDate = RevisionDate,
                Permissions = Permissions,
            };
        }
    }
}
