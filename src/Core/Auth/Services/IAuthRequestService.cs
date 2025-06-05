using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Context;

#nullable enable

namespace Bit.Core.Auth.Services;

public interface IAuthRequestService
{
    Task<AuthRequest?> GetAuthRequestAsync(Guid id, Guid userId);
    Task<AuthRequest?> GetValidatedAuthRequestAsync(Guid id, string code);
    /// <summary>
    /// Validates and Creates an <see cref="AuthRequest" /> in the database, as well as pushes it through notifications services
    /// </summary>
    /// <remarks>
    /// This method can only be called inside of an HTTP call because of it's reliance on <see cref="ICurrentContext" />
    /// </remarks>
    Task<AuthRequest> CreateAuthRequestAsync(AuthRequestCreateRequestModel model);
    Task<AuthRequest> UpdateAuthRequestAsync(Guid authRequestId, Guid userId, AuthRequestUpdateRequestModel model);
}
