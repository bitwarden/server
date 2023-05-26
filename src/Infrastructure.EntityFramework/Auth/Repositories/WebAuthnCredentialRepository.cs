using AutoMapper;
using Bit.Core.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class WebAuthnCredentialRepository : Repository<Core.Auth.Entities.WebAuthnCredential, WebAuthnCredential, Guid>, IWebAuthnCredentialRepository
{
    public WebAuthnCredentialRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (context) => context.WebAuthnCredentials)
    { }

    public async Task<Core.Auth.Entities.WebAuthnCredential> GetByIdAsync(Guid id, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.WebAuthnCredentials.Where(d => d.Id == id && d.UserId == userId);
            var cred = await query.FirstOrDefaultAsync();
            return Mapper.Map<Core.Auth.Entities.WebAuthnCredential>(cred);
        }
    }

    public async Task<ICollection<Core.Auth.Entities.WebAuthnCredential>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.WebAuthnCredentials.Where(d => d.UserId == userId);
            var creds = await query.ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.WebAuthnCredential>>(creds);
        }
    }
}
