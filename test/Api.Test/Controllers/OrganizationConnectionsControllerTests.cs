using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Api.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response.Organizations;
using Bit.Api.Test.AutoFixture.Attributes;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers
{
    [ControllerCustomize(typeof(OrganizationConnectionsController))]
    [SutProviderCustomize]
    public class OrganizationConnectionsControllerTests
    {
        public static IEnumerable<object[]> ConnectionTypes =>
            Enum.GetValues<OrganizationConnectionType>().Select(p => new object[] { p });


        [Theory]
        [BitAutoData]
        public async Task CreateConnection_RequiresOwnerPermissions(SutProvider<OrganizationConnectionsController> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateConnection(null));

            Assert.Contains("Only the owner of an organization can create a connection.", exception.Message);
        }

        [Theory]
        [BitMemberAutoData(nameof(ConnectionTypes))]
        public async Task CreateConnection_OnlyOneConnectionOfEachType(OrganizationConnectionType type,
            OrganizationConnectionRequestModel model, Guid existingEntityId,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            model.Type = type;
            var existing = model.ToData(existingEntityId).ToEntity();

            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.OrganizationId).Returns(true);

            sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByOrganizationIdTypeAsync(model.OrganizationId, type).Returns(new[] { existing });

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateConnection(model));

            Assert.Contains($"The requested organization already has a connection of type {model.Type}. Only one of each connection type may exist per organization.", exception.Message);
        }

        [Theory]
        [BitAutoData]
        public async Task CreateConnection_Success(OrganizationConnectionRequestModel model,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            sutProvider.GetDependency<ICreateOrganizationConnectionCommand>().CreateAsync(default)
                .ReturnsForAnyArgs(model.ToData(Guid.NewGuid()).ToEntity());
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.OrganizationId).Returns(true);

            await sutProvider.Sut.CreateConnection(model);

            await sutProvider.GetDependency<ICreateOrganizationConnectionCommand>().Received(1)
                .CreateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(model.ToData())));
        }

        [Theory]
        [BitAutoData]
        public async Task UpdateConnection_RequiresOwnerPermissions(SutProvider<OrganizationConnectionsController> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateConnection(default, null));

            Assert.Contains("Only the owner of an organization can update a connection.", exception.Message);
        }

        [Theory]
        [BitMemberAutoData(nameof(ConnectionTypes))]
        public async Task UpdateConnection_OnlyOneConnectionOfEachType(OrganizationConnectionType type,
            OrganizationConnection existing1, OrganizationConnection existing2,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            var model = RequestModelFromEntity(existing1);

            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.OrganizationId).Returns(true);

            sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByOrganizationIdTypeAsync(model.OrganizationId, type).Returns(new[] { existing1, existing2 });

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateConnection(existing1.Id, model));

            Assert.Contains($"The requested organization already has a connection of type {model.Type}. Only one of each connection type may exist per organization.", exception.Message);
        }

        [Theory]
        [BitAutoData]
        public async Task UpdateConnection_Success(OrganizationConnection existing, OrganizationConnection updated,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            updated.Id = existing.Id;
            var model = RequestModelFromEntity(updated);

            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.OrganizationId).Returns(true);
            sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByOrganizationIdTypeAsync(model.OrganizationId, model.Type).Returns(new[] { existing });
            sutProvider.GetDependency<IUpdateOrganizationConnectionCommand>().UpdateAsync(default).ReturnsForAnyArgs(updated);

            var result = await sutProvider.Sut.UpdateConnection(existing.Id, model);

            AssertHelper.AssertPropertyEqual(updated, result);
            await sutProvider.GetDependency<IUpdateOrganizationConnectionCommand>().Received(1)
                .UpdateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(model.ToData(updated.Id))));
        }

        [Theory]
        [BitAutoData]
        public async Task GetConnection_RequiresOwnerPermissions(Guid connectionId, SutProvider<OrganizationConnectionsController> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.GetConnection(connectionId, OrganizationConnectionType.CloudBillingSync));

            Assert.Contains("Only the owner of an organization can retrieve a connection.", exception.Message);
        }

        [Theory]
        [BitAutoData]
        public async Task GetConnection_Success(OrganizationConnection connection,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            var expected = new OrganizationConnectionResponseModel(connection);
            sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByOrganizationIdTypeAsync(connection.OrganizationId, connection.Type).Returns(new[] { connection });
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(connection.OrganizationId).Returns(true);

            var actual = await sutProvider.Sut.GetConnection(connection.OrganizationId, connection.Type);

            AssertHelper.AssertPropertyEqual(expected, actual);
        }

        [Theory]
        [BitAutoData]
        public async Task DeleteConnection_NotFound(Guid connectionId,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteConnection(connectionId));
        }

        [Theory]
        [BitAutoData]
        public async Task DeleteConnection_RequiresOwnerPermissions(OrganizationConnection connection,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByIdAsync(connection.Id).Returns(connection);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteConnection(connection.Id));

            Assert.Contains("Only the owner of an organization can remove a connection.", exception.Message);
        }

        [Theory]
        [BitAutoData]
        public async Task DeleteConnection_Success(OrganizationConnection connection,
            SutProvider<OrganizationConnectionsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByIdAsync(connection.Id).Returns(connection);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(connection.OrganizationId).Returns(true);

            await sutProvider.Sut.DeleteConnection(connection.Id);

            await sutProvider.GetDependency<IDeleteOrganizationConnectionCommand>().DeleteAsync(connection);
        }

        private OrganizationConnectionRequestModel RequestModelFromEntity(OrganizationConnection entity)
        {
            return new()
            {
                Type = entity.Type,
                OrganizationId = entity.OrganizationId,
                Enabled = entity.Enabled,
                Config = entity.Config,
            };
        }
    }
}
