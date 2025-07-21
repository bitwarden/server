using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Exceptions;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Settings;

#nullable enable

namespace Bit.Core.Auth.Services;

public interface IAuthRequestService
{
    /// <summary>
    /// Fetches an authRequest by Id. Returns AuthRequest if AuthRequest.UserId mateches
    /// userId. Returns null if the user doesn't match or if the AuthRequest is not found.
    /// </summary>
    /// <param name="authRequestId">Authrequest Id being fetched</param>
    /// <param name="userId">user who owns AuthRequest</param>
    /// <returns>An AuthRequest or null</returns>
    Task<AuthRequest?> GetAuthRequestAsync(Guid authRequestId, Guid userId);
    /// <summary>
    /// Fetches the authrequest from the database with the id provided. Then checks
    /// the accessCode against the AuthRequest.AccessCode from the database. accessCodes
    /// must match the found authRequest, and the AuthRequest must not be expired. Expiration
    /// is configured in <see cref="GlobalSettings"/>
    /// </summary>
    /// <param name="authRequestId">AuthRequest being acted on</param>
    /// <param name="accessCode">Access code of the authrequest, must match saved database value</param>
    /// <returns>A valid AuthRequest or null</returns>
    Task<AuthRequest?> GetValidatedAuthRequestAsync(Guid authRequestId, string accessCode);
    /// <summary>
    /// Validates and Creates an <see cref="AuthRequest" /> in the database, as well as pushes it through notifications services
    /// </summary>
    /// <remarks>
    /// This method can only be called inside of an HTTP call because of it's reliance on <see cref="ICurrentContext" />
    /// </remarks>
    Task<AuthRequest> CreateAuthRequestAsync(AuthRequestCreateRequestModel model);
    /// <summary>
    /// Updates the AuthRequest per the AuthRequestUpdateRequestModel context. This approves
    /// or rejects the login request.
    /// </summary>
    /// <param name="authRequestId">AuthRequest being acted on.</param>
    /// <param name="userId">User acting on AuthRequest</param>
    /// <param name="model">Update context for the AuthRequest</param>
    /// <returns>retuns an AuthRequest or throws an exception</returns>
    /// <exception cref="DuplicateAuthRequestException">Thows if the AuthRequest has already been Approved/Rejected</exception>
    /// <exception cref="NotFoundException">Throws if the AuthRequest as expired or the userId doesn't match</exception>
    /// <exception cref="BadRequestException">Throws if the device isn't associated with the UserId</exception>
    Task<AuthRequest> UpdateAuthRequestAsync(Guid authRequestId, Guid userId, AuthRequestUpdateRequestModel model);
}
