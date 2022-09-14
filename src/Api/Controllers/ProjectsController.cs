using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [SecretsManager]
    public class ProjectsController : Controller
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ICreateProjectCommand _createProjectCommand;
        private readonly IUpdateProjectCommand _updateProjectCommand;

        public ProjectsController(IProjectRepository projectRepository, ICreateProjectCommand createProjectCommand, IUpdateProjectCommand updateProjectCommand)
        {
            _projectRepository = projectRepository;
            _createProjectCommand = createProjectCommand;
            _updateProjectCommand = updateProjectCommand;
        }

        [HttpPost("organizations/{organizationId}/projects")]
        public async Task<ProjectResponseModel> CreateAsync([FromRoute] Guid organizationId, [FromBody] ProjectCreateRequestModel createRequest)
        {
            var result = await _createProjectCommand.CreateAsync(createRequest.ToProject(organizationId));
            return new ProjectResponseModel(result);
        }

        [HttpPut("projects/{id}")]
        public async Task<ProjectResponseModel> UpdateProjectAsync([FromRoute] Guid id, [FromBody] ProjectUpdateRequestModel updateRequest)
        {
            var result = await _updateProjectCommand.UpdateAsync(updateRequest.ToProject(id));
            return new ProjectResponseModel(result);
        }
    }
}
