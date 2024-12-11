using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

[Route("folders")]
[Authorize("Application")]
public class FoldersController : Controller
{
    private readonly IFolderRepository _folderRepository;
    private readonly ICipherService _cipherService;
    private readonly IUserService _userService;

    public FoldersController(
        IFolderRepository folderRepository,
        ICipherService cipherService,
        IUserService userService
    )
    {
        _folderRepository = folderRepository;
        _cipherService = cipherService;
        _userService = userService;
    }

    [HttpGet("{id}")]
    public async Task<FolderResponseModel> Get(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folder = await _folderRepository.GetByIdAsync(new Guid(id), userId);
        if (folder == null)
        {
            throw new NotFoundException();
        }

        return new FolderResponseModel(folder);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<FolderResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folders = await _folderRepository.GetManyByUserIdAsync(userId);
        var responses = folders.Select(f => new FolderResponseModel(f));
        return new ListResponseModel<FolderResponseModel>(responses);
    }

    [HttpPost("")]
    public async Task<FolderResponseModel> Post([FromBody] FolderRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folder = model.ToFolder(_userService.GetProperUserId(User).Value);
        await _cipherService.SaveFolderAsync(folder);
        return new FolderResponseModel(folder);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<FolderResponseModel> Put(string id, [FromBody] FolderRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folder = await _folderRepository.GetByIdAsync(new Guid(id), userId);
        if (folder == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveFolderAsync(model.ToFolder(folder));
        return new FolderResponseModel(folder);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folder = await _folderRepository.GetByIdAsync(new Guid(id), userId);
        if (folder == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteFolderAsync(folder);
    }

    [HttpDelete("all")]
    public async Task DeleteAll()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var allFolders = await _folderRepository.GetManyByUserIdAsync(userId);

        foreach (var folder in allFolders)
        {
            await _cipherService.DeleteFolderAsync(folder);
        }
    }
}
