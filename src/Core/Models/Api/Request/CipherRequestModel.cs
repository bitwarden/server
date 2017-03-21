using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;

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
        [StringLength(36)]
        public string FolderId { get; set; }
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

        public virtual Cipher ToCipher(Guid userId)
        {
            return ToCipher(new Cipher
            {
                Id = new Guid(Id),
                UserId = string.IsNullOrWhiteSpace(OrganizationId) ? (Guid?)userId : null,
                Type = Type
            });
        }

        public Cipher ToCipher(Cipher existingCipher)
        {
            existingCipher.OrganizationId = string.IsNullOrWhiteSpace(OrganizationId) ? null : (Guid?)new Guid(OrganizationId);

            switch(existingCipher.Type)
            {
                case CipherType.Login:
                    existingCipher.Data = JsonConvert.SerializeObject(new LoginDataModel(this), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }

            return existingCipher;
        }
    }

    public class CipherMoveRequestModel
    {
        public IEnumerable<string> SubvaultIds { get; set; }
        [Required]
        public CipherRequestModel Cipher { get; set; }
    }
}
