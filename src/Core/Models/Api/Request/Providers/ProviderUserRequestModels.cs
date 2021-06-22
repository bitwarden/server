using System;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Models.Api
{
    public class ProviderUserInviteRequestModel : IValidatableObject
    {
        [Required]
        public IEnumerable<string> Emails { get; set; }
        [Required]
        public ProviderUserType? Type { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Emails.Any())
            {
                yield return new ValidationResult("An email is required.");
            }

            if (Emails.Count() > 20)
            {
                yield return new ValidationResult("You can only invite up to 20 users at a time.");
            }

            var attr = new EmailAddressAttribute();
            for (var i = 0; i < Emails.Count(); i++)
            {
                var email = Emails.ElementAt(i);
                if (!attr.IsValid(email) || email.Contains(" ") || email.Contains("<"))
                {
                    yield return new ValidationResult($"Email #{i + 1} is not valid.",
                        new string[] { nameof(Emails) });
                }
                else if (email.Length > 256)
                {
                    yield return new ValidationResult($"Email #{i + 1} is longer than 256 characters.",
                        new string[] { nameof(Emails) });
                }
            }
        }
    }

    public class ProviderUserAcceptRequestModel
    {
        [Required]
        public string Token { get; set; }
    }

    public class ProviderUserConfirmRequestModel
    {
        [Required]
        public string Key { get; set; }
    }

    public class ProviderUserBulkConfirmRequestModelEntry
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        public string Key { get; set; }
    }

    public class ProviderUserBulkConfirmRequestModel
    {
        [Required]
        public IEnumerable<ProviderUserBulkConfirmRequestModelEntry> Keys { get; set; }

        public Dictionary<Guid, string> ToDictionary()
        {
            return Keys.ToDictionary(e => e.Id, e => e.Key);
        }
    }

    public class ProviderUserUpdateRequestModel
    {
        [Required]
        public ProviderUserType? Type { get; set; }

        public ProviderUser ToProviderUser(ProviderUser existingUser)
        {
            existingUser.Type = Type.Value;
            return existingUser;
        }
    }

    public class ProviderUserBulkRequestModel
    {
        [Required]
        public IEnumerable<Guid> Ids { get; set; }
    }
}
