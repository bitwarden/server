﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationConnections;

[SutProviderCustomize]
public class ValidateBillingSyncKeyCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateBillingSyncKeyAsync_NullOrganization_Throws(
        SutProvider<ValidateBillingSyncKeyCommand> sutProvider
    )
    {
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateBillingSyncKeyAsync(null, null)
        );
    }

    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData("       ")]
    public async Task ValidateBillingSyncKeyAsync_BadString_ReturnsFalse(
        string billingSyncKey,
        SutProvider<ValidateBillingSyncKeyCommand> sutProvider
    )
    {
        Assert.False(
            await sutProvider.Sut.ValidateBillingSyncKeyAsync(new Organization(), billingSyncKey)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateBillingSyncKeyAsync_KeyEquals_ReturnsTrue(
        SutProvider<ValidateBillingSyncKeyCommand> sutProvider,
        Organization organization,
        OrganizationApiKey orgApiKey,
        string billingSyncKey
    )
    {
        orgApiKey.ApiKey = billingSyncKey;

        sutProvider
            .GetDependency<IOrganizationApiKeyRepository>()
            .GetManyByOrganizationIdTypeAsync(organization.Id, OrganizationApiKeyType.BillingSync)
            .Returns(new[] { orgApiKey });

        Assert.True(
            await sutProvider.Sut.ValidateBillingSyncKeyAsync(organization, billingSyncKey)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateBillingSyncKeyAsync_KeyDoesNotEqual_ReturnsFalse(
        SutProvider<ValidateBillingSyncKeyCommand> sutProvider,
        Organization organization,
        OrganizationApiKey orgApiKey,
        string billingSyncKey
    )
    {
        sutProvider
            .GetDependency<IOrganizationApiKeyRepository>()
            .GetManyByOrganizationIdTypeAsync(organization.Id, OrganizationApiKeyType.BillingSync)
            .Returns(new[] { orgApiKey });

        Assert.False(
            await sutProvider.Sut.ValidateBillingSyncKeyAsync(organization, billingSyncKey)
        );
    }
}
