using Bit.Api.Tools.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// Send implementation for <see cref="IRotationValidator{T,R}"/>
/// </summary>
public class SendRotationValidator : IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>>
{
    private readonly ISendService _sendService;
    private readonly ISendRepository _sendRepository;

    /// <summary>
    /// Instantiates a new <see cref="SendRotationValidator"/>
    /// </summary>
    /// <param name="sendService">Enables conversion of <see cref="SendWithIdRequestModel"/> to <see cref="Send"/></param>
    /// <param name="sendRepository">Retrieves all user <see cref="Send"/>s</param>
    public SendRotationValidator(ISendService sendService, ISendRepository sendRepository)
    {
        _sendService = sendService;
        _sendRepository = sendRepository;
    }

    public async Task<IReadOnlyList<Send>> ValidateAsync(User user, IEnumerable<SendWithIdRequestModel> sends)
    {
        var result = new List<Send>();

        var existingSends = await _sendRepository.GetManyByUserIdAsync(user.Id);
        if (existingSends == null || existingSends.Count == 0)
        {
            return result;
        }

        foreach (var existing in existingSends)
        {
            var send = sends.FirstOrDefault(c => c.Id == existing.Id);
            if (send == null)
            {
                throw new BadRequestException("All existing sends must be included in the rotation.");
            }

            result.Add(send.ToSend(existing, _sendService));
        }

        return result;
    }
}
