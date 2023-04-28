using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;

#nullable enable

namespace Bit.Core.Auth.Services;

public interface IAuthRequestService
{
    Task<AuthRequest?> GetAuthRequestAsync(Guid id, Guid userId);
    Task<AuthRequest?> GetValidatedAuthRequestAsync(Guid id, string code);
    Task<AuthRequest> CreateAuthRequestAsync(AuthRequestCreateRequestModel model);
    Task<AuthRequest> UpdateAuthRequestAsync(Guid authRequestId, Guid userId, AuthRequestUpdateRequestModel model)
}
