using Bit.Api.Auth;
using Bit.Api.Tools.Models.Request;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;

public class SendRotationValidator : IRotationValidator<IEnumerable<SendWithIdRequestModel>, IEnumerable<Send>>
{
    private readonly ISendRepository _sendRepository;

    public SendRotationValidator(ISendRepository sendRepository)
    {
        _sendRepository = sendRepository;
    }

    public async Task<IEnumerable<Send>> ValidateAsync(Guid userId, IEnumerable<SendWithIdRequestModel> sends)
    {
        if (!sends.Any())
        {
            return null;
        }

        var existingSends = await _sendRepository.GetManyByUserIdAsync(userId);

        throw new NotImplementedException();
    }
}
