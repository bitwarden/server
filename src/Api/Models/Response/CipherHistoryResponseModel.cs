using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class CipherHistoryResponseModel : ResponseModel
    {
        public CipherHistoryResponseModel(IEnumerable<Cipher> revisedCiphers, IEnumerable<Guid> deletedIds)
            : base("cipherHistory")
        {
            if(revisedCiphers == null)
            {
                throw new ArgumentNullException(nameof(revisedCiphers));
            }

            if(deletedIds == null)
            {
                throw new ArgumentNullException(nameof(deletedIds));
            }

            Revised = revisedCiphers.Select(c => new CipherResponseModel(c));
            Deleted = deletedIds.Select(id => id.ToString());
        }

        public IEnumerable<CipherResponseModel> Revised { get; set; }
        public IEnumerable<string> Deleted { get; set; }
    }
}
