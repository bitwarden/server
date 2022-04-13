using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [SelfHosted(SelfHostedOnly = true)]
    [Authorize("Application")]
    [Route("organization/connection")]
    public class OrganizationConnectionsController : Controller
    {
        private readonly ICreateOrganizationConnectionCommand _createOrganizationConnectionCommand;
        private readonly IUpdateOrganizationConnectionCommand _updateOrganizationConnectionCommand;
        private readonly IDeleteOrganizationConnectionCommand _deleteOrganizationConnectionCommand;
        private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
        private readonly ICurrentContext _currentContext;

        public OrganizationConnectionsController(
            ICreateOrganizationConnectionCommand createOrganizationConnectionCommand,
            IUpdateOrganizationConnectionCommand updateOrganizationConnectionCommand,
            IDeleteOrganizationConnectionCommand deleteOrganizationConnectionCommand,
            IOrganizationConnectionRepository organizationConnectionRepository,
            ICurrentContext currentContext)
        {
            _createOrganizationConnectionCommand = createOrganizationConnectionCommand;
            _updateOrganizationConnectionCommand = updateOrganizationConnectionCommand;
            _deleteOrganizationConnectionCommand = deleteOrganizationConnectionCommand;
            _organizationConnectionRepository = organizationConnectionRepository;
            _currentContext = currentContext;
        }


        [HttpPost("")]
        public async Task<OrganizationConnectionResponseModel> CreateConnection([FromBody] OrganizationConnectionRequestModel model)
        {
            if (!await HasPermissionAsync(model?.OrganizationId))
            {
                throw new BadRequestException("Only the owner of an organization can create a connection.");
            }

            if (await HasConnectionTypeAsync(model))
            {
                throw new BadRequestException($"The requested organization already has a connection of type {model.Type}. Only one of each connection type may exist per organization.");
            }

            var connection = await _createOrganizationConnectionCommand.CreateAsync(model.ToData());
            return new OrganizationConnectionResponseModel(connection);
        }

        [HttpPut("{organizationConnectionId}")]
        public async Task<OrganizationConnectionResponseModel> UpdateConnection(Guid organizationConnectionId, [FromBody] OrganizationConnectionRequestModel model)
        {
            if (!await HasPermissionAsync(model?.OrganizationId))
            {
                throw new BadRequestException("Only the owner of an organization can update a connection.");
            }

            if (await HasConnectionTypeAsync(model, organizationConnectionId))
            {
                throw new BadRequestException($"The requested organization already has a connection of type {model.Type}. Only one of each connection type may exist per organization.");
            }

            var connection = await _updateOrganizationConnectionCommand.UpdateAsync(model.ToData(organizationConnectionId));
            return new OrganizationConnectionResponseModel(connection);
        }

        [HttpGet("{organizationId}/{type}")]
        public async Task<OrganizationConnectionResponseModel> GetConnection(Guid organizationId, OrganizationConnectionType type)
        {
            if (!await HasPermissionAsync(organizationId))
            {
                throw new BadRequestException("Only the owner of an organization can retrieve a connection.");
            }

            var connections = await GetConnectionsAsync(organizationId);
            return new OrganizationConnectionResponseModel(connections.FirstOrDefault(c => c.Type == type));
        }

        [HttpDelete("{organizationConnectionId}")]
        [HttpPost("{organizationConnectionId}}/delete")]
        public async Task DeleteConnection(Guid organizationConnectionId)
        {
            var connection = await _organizationConnectionRepository.GetByIdAsync(organizationConnectionId);

            if (connection == null)
            {
                throw new NotFoundException();
            }

            if (!await HasPermissionAsync(connection.OrganizationId))
            {
                throw new BadRequestException("Only the owner of an organization can remove a connection.");
            }

            await _deleteOrganizationConnectionCommand.DeleteAsync(connection);
        }

        private async Task<ICollection<OrganizationConnection>> GetConnectionsAsync(Guid organizationId) =>
            await _organizationConnectionRepository.GetEnabledByOrganizationIdTypeAsync(organizationId, OrganizationConnectionType.CloudBillingSync);

        private async Task<bool> HasConnectionTypeAsync(OrganizationConnectionRequestModel model, Guid? connectionId = null)
        {
            var existingConnections = await GetConnectionsAsync(model.OrganizationId);

            return existingConnections.Any(c => c.Type == model.Type && (!connectionId.HasValue || c.Id != connectionId.Value));
        }

        private async Task<bool> HasPermissionAsync(Guid? organizationId) =>
            organizationId.HasValue && await _currentContext.OrganizationOwner(organizationId.Value);
    }
}
