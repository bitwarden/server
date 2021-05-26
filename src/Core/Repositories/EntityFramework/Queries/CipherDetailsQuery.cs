using System.Linq;
using Bit.Core.Models.Table;
using System;
using Bit.Core.Enums;
using System.Collections.Generic;
using Core.Models.Data;
using System.Text.Json;
using Bit.Core.Utilities;
using System.Threading.Tasks;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CipherDetailsQuery : IQuery<CipherDetails>
    {
        private Guid? UserId { get; set; }
        private bool IgnoreFolders { get; set; } 
        public CipherDetailsQuery(Guid? userId, bool ignoreFolders = false)
        {
            UserId = userId;
            IgnoreFolders = ignoreFolders;
        }
        public virtual IQueryable<CipherDetails> Run(DatabaseContext dbContext)
        {
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
                            Favorite = UserId.HasValue && c.Favorites != null && c.Favorites.Contains($"\"{UserId}\":true"),
                            FolderId = (IgnoreFolders || !UserId.HasValue || c.Folders == null || !c.Folders.Contains(UserId.Value.ToString())) ? 
                                null : 
                                CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(c.Folders)[UserId.Value]
                        };
            return query;
        }
    }
}
