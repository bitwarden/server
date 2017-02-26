using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Domains;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class ShareRequestModel
    {
        [Required]
        public string Email { get; set; }
        [Required]
        [StringLength(36)]
        public string CipherId { get; set; }
        public string Key { get; set; }

        public Share ToShare(Guid sharerUserId)
        {
            return ToShare(new Share
            {
                SharerUserId = sharerUserId
            });
        }

        public Share ToShare(Share existingShare)
        {
            existingShare.CipherId = new Guid(CipherId);
            existingShare.Key = Key;

            return existingShare;
        }
    }
}
