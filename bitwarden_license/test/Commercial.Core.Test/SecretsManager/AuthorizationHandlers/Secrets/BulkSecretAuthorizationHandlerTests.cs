using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.Secrets;

[SutProviderCustomize]
[ProjectCustomize]
public class BulkSecretAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData]
    public async Task HandleAsync_DifferentOrganizations_DoesNotSucceed(
      SutProvider<BulkSecretAuthorizationHandler> sutProvider,
      ClaimsPrincipal claimsPrincipal,
      Secret secret1,
      Secret secret2)
    {
        var secrets = new List<Secret> { secret1, secret2 };

        var authorizationContext = new AuthorizationHandlerContext(
          new List<IAuthorizationRequirement> { BulkSecretOperations.Update },
          claimsPrincipal,
          secrets);

        await sutProvider.Sut.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_DoesNotHaveAccessToSm_DoesNotSucceed(
      SutProvider<BulkSecretAuthorizationHandler> sutProvider,
      ClaimsPrincipal claimsPrincipal,
      Secret secret1,
      Secret secret2)
    {
        secret2.OrganizationId = secret1.OrganizationId;

        sutProvider.GetDependency<ICurrentContext>()
          .AccessSecretsManager(secret1.OrganizationId)
          .Returns(false);

        var secrets = new List<Secret> { secret1, secret2 };

        var authorizationContext = new AuthorizationHandlerContext(
          new List<IAuthorizationRequirement> { BulkSecretOperations.Update },
          claimsPrincipal,
          secrets);

        await sutProvider.Sut.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_UnknownRequirement_DoesNotSucceed(
      SutProvider<BulkSecretAuthorizationHandler> sutProvider,
      ClaimsPrincipal claimsPrincipal,
      Secret secret1,
      Secret secret2,
      Guid userId)
    {
        secret2.OrganizationId = secret1.OrganizationId;

        sutProvider.GetDependency<ICurrentContext>()
          .AccessSecretsManager(secret1.OrganizationId)
          .Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
          .GetAccessClientAsync(claimsPrincipal, secret1.OrganizationId)
          .Returns((AccessClientType.User, userId));

        var secrets = new List<Secret> { secret1, secret2 };

        var authorizationContext = new AuthorizationHandlerContext(
          new List<IAuthorizationRequirement> { new BulkSecretOperationRequirement { Name = "Something" } },
          claimsPrincipal,
          secrets);

        await sutProvider.Sut.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_DoesNotHaveWriteAccessToAllSecrets_DoesNotSucceed(
      SutProvider<BulkSecretAuthorizationHandler> sutProvider,
      ClaimsPrincipal claimsPrincipal,
      Secret secret1,
      Secret secret2,
      Guid userId)
    {
        secret2.OrganizationId = secret1.OrganizationId;

        sutProvider.GetDependency<ICurrentContext>()
          .AccessSecretsManager(secret1.OrganizationId)
          .Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
          .GetAccessClientAsync(claimsPrincipal, secret1.OrganizationId)
          .Returns((AccessClientType.User, userId));

        sutProvider.GetDependency<ISecretRepository>()
          .AccessToSecretsAsync(
            Arg.Is<Guid[]>(arr => arr.Length == 2 && arr[0] == secret1.Id && arr[1] == secret2.Id),
            userId,
            AccessClientType.User
          )
          .Returns(new Dictionary<Guid, (bool Read, bool Write)>
          {
              [secret1.Id] = (true, false),
              [secret2.Id] = (true, true),
          });

        var secrets = new List<Secret> { secret1, secret2 };

        var authorizationContext = new AuthorizationHandlerContext(
          new List<IAuthorizationRequirement> { BulkSecretOperations.Update },
          claimsPrincipal,
          secrets);

        await sutProvider.Sut.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_HasWriteAccessToAllSecrets_Succeeds(
      SutProvider<BulkSecretAuthorizationHandler> sutProvider,
      ClaimsPrincipal claimsPrincipal,
      Secret secret1,
      Secret secret2,
      Guid userId)
    {
        secret2.OrganizationId = secret1.OrganizationId;

        sutProvider.GetDependency<ICurrentContext>()
          .AccessSecretsManager(secret1.OrganizationId)
          .Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
          .GetAccessClientAsync(claimsPrincipal, secret1.OrganizationId)
          .Returns((AccessClientType.User, userId));

        sutProvider.GetDependency<ISecretRepository>()
          .AccessToSecretsAsync(
            Arg.Is<Guid[]>(arr => arr.Length == 2 && arr[0] == secret1.Id && arr[1] == secret2.Id),
            userId,
            AccessClientType.User
          )
          .Returns(new Dictionary<Guid, (bool Read, bool Write)>
          {
              [secret1.Id] = (true, true),
              [secret2.Id] = (true, true),
          });

        var secrets = new List<Secret> { secret1, secret2 };

        var authorizationContext = new AuthorizationHandlerContext(
          new List<IAuthorizationRequirement> { BulkSecretOperations.Update },
          claimsPrincipal,
          secrets);

        await sutProvider.Sut.HandleAsync(authorizationContext);

        Assert.True(authorizationContext.HasSucceeded);
    }
}
