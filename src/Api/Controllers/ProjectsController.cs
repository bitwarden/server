using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [SecretsManager]
    public class ProjectsController : Controller
    {
        private readonly IProjectRepository _projectRepository;

        public ProjectsController(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
        }

        // [HttpGet("organizations/{organizationId}/projects")]
        // public async Task<ListResponseModel<ProjectIdentifierResponseModel>> GetProjectsByOrganizationAsync([FromRoute] Guid organizationId)
        // {
        //     var projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId);
        //     if (projects == null || !projects.Any())
        //     {
        //         throw new NotFoundException();
        //     }
        //     var responses = projects.Select(project => new SecretIdentifierResponseModel(project));
        //     return new ListResponseModel<SecretIdentifierResponseModel>(responses);
        // }


        // [HttpGet("secrets/{id}")]
        // public async Task<SecretResponseModel> GetSecretAsync([FromRoute] Guid id)
        // {
        //     var secret = await _secretRepository.GetByIdAsync(id);
        //     if (secret == null)
        //     {
        //         throw new NotFoundException();
        //     }
        //     return new SecretResponseModel(secret);
        // }

        // [HttpPost("organizations/{organizationId}/secrets")]
        // public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
        // {
        //     if (organizationId != createRequest.OrganizationId)
        //     {
        //         throw new BadRequestException("Organization ID does not match.");
        //     }

        //     var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret());
        //     return new SecretResponseModel(result);
        // }

        // [HttpPut("secrets/{id}")]
        // public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
        // {
        //     var result = await _updateSecretCommand.UpdateAsync(updateRequest.ToSecret(id));
        //     return new SecretResponseModel(result);
        // }
    }
}
