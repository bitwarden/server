using Bit.Api.Auth;
using Bit.Api.Tools.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;

namespace Bit.Api.Tools;

public class SendRotationValidator : IRotationValidator<IEnumerable<SendWithIdRequestModel>, IEnumerable<Send>>
{
    private readonly ISendService _sendService;
    private readonly ISendRepository _sendRepository;

    public SendRotationValidator(ISendService sendService, ISendRepository sendRepository)
    {
        _sendService = sendService;
        _sendRepository = sendRepository;
    }

    public async Task<IEnumerable<Send>> ValidateAsync(Guid userId, IEnumerable<SendWithIdRequestModel> sends)
    {
        if (!sends.Any())
        {
            return null;
        }

        var existingSends = await _sendRepository.GetManyByUserIdAsync(userId);
        var result = new List<Send>();

        foreach (var existing in existingSends)
        {
            var send = sends.FirstOrDefault(c => c.Id == existing.Id);
            if (send == null)
            {
                throw new BadRequestException("All existing folders must be included in the rotation.");
            }
            result.Add(send.ToSend(existing, _sendService));
        }
        return result;
    }
}
