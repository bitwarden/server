using System.Security.Claims;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReceiveFeatures.Queries.Interfaces;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;

namespace Bit.Core.Tools.ReceiveFeatures.Queries;

/// <inheritdoc cref="IGetReceiveFileDownloadQuery"/>
public class GetReceiveFileDownloadQuery : IGetReceiveFileDownloadQuery
{
    private readonly IReceiveRepository _repository;
    private readonly IUserService _userService;
    private readonly IReceiveFileStorageService _fileStorageService;

    public GetReceiveFileDownloadQuery(
        IReceiveRepository repository,
        IUserService userService,
        IReceiveFileStorageService fileStorageService)
    {
        _repository = repository;
        _userService = userService;
        _fileStorageService = fileStorageService;
    }

    /// <inheritdoc />
    public async Task<string> GetDownloadUrlAsync(Guid receiveId, string fileId, ClaimsPrincipal user)
    {
        var userId = _userService.GetProperUserId(user) ?? throw new BadRequestException("invalid user.");
        var receive = await _repository.GetByIdAsync(receiveId);

        if (receive == null || receive.UserId != userId)
        {
            throw new NotFoundException();
        }

        var receiveData = JsonSerializer.Deserialize<ReceiveData>(receive.Data);
        var file = receiveData?.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null)
        {
            throw new NotFoundException();
        }

        return await _fileStorageService.GetReceiveFileDownloadUrlAsync(receive, fileId);
    }
}
