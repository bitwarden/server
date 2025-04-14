﻿using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IUpdateSecretsManagerSubscriptionCommand
{
    Task UpdateSubscriptionAsync(SecretsManagerSubscriptionUpdate update);
    Task ValidateUpdateAsync(SecretsManagerSubscriptionUpdate update);
}
