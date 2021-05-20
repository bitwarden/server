using System.Linq;
using Bit.Core.Models.Table;
using System;
using Bit.Core.Enums;
using System.Collections.Generic;
using Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CipherDetailsQuery : IQuery<CipherDetails>
    {
        private Guid? UserId { get; set; }
        public CipherDetailsQuery(Guid? userId)
        {
            UserId = userId;
        }
        public virtual IQueryable<CipherDetails> Run(DatabaseContext dbContext)
        {
            // TODO: set FolderId appropriatly 
            var query = from c in dbContext.Ciphers
                        select new CipherDetails() { 
                            Id = c.Id,
                            UserId = c.UserId,
                            OrganizationId = c.OrganizationId,
                            Type= c.Type,
                            Data = c.Data,
                            Attachments = c.Attachments,
                            CreationDate = c.CreationDate,
                            RevisionDate = c.RevisionDate,
                            DeletedDate = c.DeletedDate,
                            Favorite = UserId.HasValue && c.Favorites != null && c.Favorites.Contains($"$.\"{UserId}\""),
                            FolderId = !UserId.HasValue || c.Folders == null ? null : null
                        };
            return query;
        }
    }
}
