using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class CipherRequestModel
    {
        public CipherType Type { get; set; }

        [Required]
        [StringLength(36)]
        public string Id { get; set; }
        [StringLength(36)]
        public string OrganizationId { get; set; }
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }
        [EncryptedString]
        [StringLength(10000)]
        public string Uri { get; set; }
        [EncryptedString]
        [StringLength(300)]
        public string Username { get; set; }
        [EncryptedString]
        [StringLength(300)]
        public string Password { get; set; }
        [EncryptedString]
        [StringLength(10000)]
        public string Notes { get; set; }
        [EncryptedString]
        [StringLength(300)]
        public string Totp { get; set; }

        public virtual Cipher ToCipher(Guid userId)
        {
            return ToCipher(new Cipher
            {
                Id = new Guid(Id),
                UserId = string.IsNullOrWhiteSpace(OrganizationId) ? (Guid?)userId : null,
                Type = Type
            });
        }

        public virtual Cipher ToCipher(Cipher existingCipher)
        {
            switch(existingCipher.Type)
            {
                case CipherType.Login:
                    existingCipher.Data = JsonConvert.SerializeObject(new LoginDataModel(this),
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }

            return existingCipher;
        }
    }

    public class CipherAttachmentRequestModel : CipherRequestModel
    {
        public Dictionary<string, string> Attachments { get; set; }

        public override Cipher ToCipher(Cipher existingCipher)
        {
            base.ToCipher(existingCipher);

            var attachments = existingCipher.GetAttachments();
            if((Attachments?.Count ?? 0) > 0 && (attachments?.Count ?? 0) > 0)
            {
                foreach(var attachment in existingCipher.GetAttachments().Where(a => Attachments.ContainsKey(a.Key)))
                {
                    attachment.Value.FileName = Attachments[attachment.Key];
                }

                existingCipher.SetAttachments(attachments);
            }

            return existingCipher;
        }
    }

    public class CipherShareRequestModel : IValidatableObject
    {
        [Required]
        public IEnumerable<string> CollectionIds { get; set; }
        [Required]
        public CipherAttachmentRequestModel Cipher { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(string.IsNullOrWhiteSpace(Cipher.OrganizationId))
            {
                yield return new ValidationResult("Cipher OrganizationId is required.",
                    new string[] { nameof(Cipher.OrganizationId) });
            }

            if(!CollectionIds?.Any() ?? false)
            {
                yield return new ValidationResult("You must select at least one collection.",
                    new string[] { nameof(CollectionIds) });
            }
        }
    }

    public class CipherCollectionsRequestModel
    {
        [Required]
        public IEnumerable<string> CollectionIds { get; set; }
    }

    public class CipherBulkDeleteRequestModel
    {
        [Required]
        public IEnumerable<string> Ids { get; set; }
    }

    public class CipherBulkMoveRequestModel
    {
        [Required]
        public IEnumerable<string> Ids { get; set; }
        public string FolderId { get; set; }
    }
}
