using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class CompleteSubscriptionUpdateTests
{
    [Theory]
    [BitAutoData]
    [TeamsStarterOrganizationCustomize]
    public void UpgradeItemOptions_TeamsStarterToTeams_ReturnsCorrectOptions(
        Organization organization)
    {
        var teamsStarterPlan = StaticStore.GetPlan(PlanType.TeamsStarter);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "subscription_item",
                        Price = new Price { Id = teamsStarterPlan.PasswordManager.StripePlanId },
                        Quantity = 1
                    }
                }
            }
        };

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        var updatedSubscriptionData = new SubscriptionData
        {
            Plan = teamsMonthlyPlan,
            PurchasedPasswordManagerSeats = 20
        };

        var subscriptionUpdate = new CompleteSubscriptionUpdate(organization, updatedSubscriptionData);

        var upgradeItemOptions = subscriptionUpdate.UpgradeItemsOptions(subscription);

        Assert.Single(upgradeItemOptions);

        var passwordManagerOptions = upgradeItemOptions.First();

        Assert.Equal(subscription.Items.Data.FirstOrDefault()?.Id, passwordManagerOptions.Id);
        Assert.Equal(teamsMonthlyPlan.PasswordManager.StripeSeatPlanId, passwordManagerOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedPasswordManagerSeats, passwordManagerOptions.Quantity);
        Assert.Null(passwordManagerOptions.Deleted);
    }

    [Theory]
    [BitAutoData]
    [TeamsMonthlyWithAddOnsOrganizationCustomize]
    public void UpgradeItemOptions_TeamsWithSMToEnterpriseWithSM_ReturnsCorrectOptions(
        Organization organization)
    {
        // 5 purchased, 1 base
        organization.MaxStorageGb = 6;

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "password_manager_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.PasswordManager.StripeSeatPlanId },
                        Quantity = organization.Seats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.SecretsManager.StripeSeatPlanId },
                        Quantity = organization.SmSeats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_service_accounts_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.SecretsManager.StripeServiceAccountPlanId },
                        Quantity = organization.SmServiceAccounts!.Value
                    },
                    new ()
                    {
                        Id = "password_manager_storage_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.PasswordManager.StripeStoragePlanId },
                        Quantity = organization.Storage!.Value
                    }
                }
            }
        };

        var enterpriseMonthlyPlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var updatedSubscriptionData = new SubscriptionData
        {
            Plan = enterpriseMonthlyPlan,
            PurchasedPasswordManagerSeats = 50,
            SubscribedToSecretsManager = true,
            PurchasedSecretsManagerSeats = 30,
            PurchasedAdditionalSecretsManagerServiceAccounts = 10,
            PurchasedAdditionalStorage = 10
        };

        var subscriptionUpdate = new CompleteSubscriptionUpdate(organization, updatedSubscriptionData);

        var upgradeItemOptions = subscriptionUpdate.UpgradeItemsOptions(subscription);

        Assert.Equal(4, upgradeItemOptions.Count);

        var passwordManagerOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.PasswordManager.StripeSeatPlanId);

        var passwordManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_subscription_item");

        Assert.Equal(passwordManagerSubscriptionItem?.Id, passwordManagerOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.PasswordManager.StripeSeatPlanId, passwordManagerOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedPasswordManagerSeats, passwordManagerOptions.Quantity);
        Assert.Null(passwordManagerOptions.Deleted);

        var secretsManagerOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.SecretsManager.StripeSeatPlanId);

        var secretsManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "secrets_manager_subscription_item");

        Assert.Equal(secretsManagerSubscriptionItem?.Id, secretsManagerOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.SecretsManager.StripeSeatPlanId, secretsManagerOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedSecretsManagerSeats, secretsManagerOptions.Quantity);
        Assert.Null(secretsManagerOptions.Deleted);

        var serviceAccountsOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.SecretsManager.StripeServiceAccountPlanId);

        var serviceAccountsSubscriptionItem = subscription.Items.Data.FirstOrDefault(item =>
            item.Id == "secrets_manager_service_accounts_subscription_item");

        Assert.Equal(serviceAccountsSubscriptionItem?.Id, serviceAccountsOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.SecretsManager.StripeServiceAccountPlanId, serviceAccountsOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedAdditionalSecretsManagerServiceAccounts, serviceAccountsOptions.Quantity);
        Assert.Null(serviceAccountsOptions.Deleted);

        var storageOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.PasswordManager.StripeStoragePlanId);

        var storageSubscriptionItem = subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_storage_subscription_item");

        Assert.Equal(storageSubscriptionItem?.Id, storageOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.PasswordManager.StripeStoragePlanId, storageOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedAdditionalStorage, storageOptions.Quantity);
        Assert.Null(storageOptions.Deleted);
    }

    [Theory]
    [BitAutoData]
    [TeamsMonthlyWithAddOnsOrganizationCustomize]
    public void UpgradeItemOptions_TeamsWithSMToEnterpriseWithoutSM_ReturnsCorrectOptions(
        Organization organization)
    {
        // 5 purchased, 1 base
        organization.MaxStorageGb = 6;

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "password_manager_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.PasswordManager.StripeSeatPlanId },
                        Quantity = organization.Seats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.SecretsManager.StripeSeatPlanId },
                        Quantity = organization.SmSeats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_service_accounts_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.SecretsManager.StripeServiceAccountPlanId },
                        Quantity = organization.SmServiceAccounts!.Value
                    },
                    new ()
                    {
                        Id = "password_manager_storage_subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.PasswordManager.StripeStoragePlanId },
                        Quantity = organization.Storage!.Value
                    }
                }
            }
        };

        var enterpriseMonthlyPlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var updatedSubscriptionData = new SubscriptionData
        {
            Plan = enterpriseMonthlyPlan,
            PurchasedPasswordManagerSeats = 50,
            SubscribedToSecretsManager = false,
            PurchasedSecretsManagerSeats = 0,
            PurchasedAdditionalSecretsManagerServiceAccounts = 0,
            PurchasedAdditionalStorage = 10
        };

        var subscriptionUpdate = new CompleteSubscriptionUpdate(organization, updatedSubscriptionData);

        var upgradeItemOptions = subscriptionUpdate.UpgradeItemsOptions(subscription);

        Assert.Equal(4, upgradeItemOptions.Count);

        var passwordManagerOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.PasswordManager.StripeSeatPlanId);

        var passwordManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_subscription_item");

        Assert.Equal(passwordManagerSubscriptionItem?.Id, passwordManagerOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.PasswordManager.StripeSeatPlanId, passwordManagerOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedPasswordManagerSeats, passwordManagerOptions.Quantity);
        Assert.Null(passwordManagerOptions.Deleted);

        var secretsManagerOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.SecretsManager.StripeSeatPlanId);

        var secretsManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "secrets_manager_subscription_item");

        Assert.Equal(secretsManagerSubscriptionItem?.Id, secretsManagerOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.SecretsManager.StripeSeatPlanId, secretsManagerOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedSecretsManagerSeats, secretsManagerOptions.Quantity);
        Assert.True(secretsManagerOptions.Deleted);

        var serviceAccountsOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.SecretsManager.StripeServiceAccountPlanId);

        var serviceAccountsSubscriptionItem = subscription.Items.Data.FirstOrDefault(item =>
            item.Id == "secrets_manager_service_accounts_subscription_item");

        Assert.Equal(serviceAccountsSubscriptionItem?.Id, serviceAccountsOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.SecretsManager.StripeServiceAccountPlanId, serviceAccountsOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedAdditionalSecretsManagerServiceAccounts, serviceAccountsOptions.Quantity);
        Assert.True(serviceAccountsOptions.Deleted);

        var storageOptions = upgradeItemOptions.FirstOrDefault(options =>
            options.Price == enterpriseMonthlyPlan.PasswordManager.StripeStoragePlanId);

        var storageSubscriptionItem = subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_storage_subscription_item");

        Assert.Equal(storageSubscriptionItem?.Id, storageOptions!.Id);
        Assert.Equal(enterpriseMonthlyPlan.PasswordManager.StripeStoragePlanId, storageOptions.Price);
        Assert.Equal(updatedSubscriptionData.PurchasedAdditionalStorage, storageOptions.Quantity);
        Assert.Null(storageOptions.Deleted);
    }

    [Theory]
    [BitAutoData]
    [TeamsStarterOrganizationCustomize]
    public void RevertItemOptions_TeamsStarterToTeams_ReturnsCorrectOptions(
        Organization organization)
    {
        var teamsStarterPlan = StaticStore.GetPlan(PlanType.TeamsStarter);
        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "subscription_item",
                        Price = new Price { Id = teamsMonthlyPlan.PasswordManager.StripeSeatPlanId },
                        Quantity = 20
                    }
                }
            }
        };

        var updatedSubscriptionData = new SubscriptionData
        {
            Plan = teamsMonthlyPlan,
            PurchasedPasswordManagerSeats = 20
        };

        var subscriptionUpdate = new CompleteSubscriptionUpdate(organization, updatedSubscriptionData);

        var revertItemOptions = subscriptionUpdate.RevertItemsOptions(subscription);

        Assert.Single(revertItemOptions);

        var passwordManagerOptions = revertItemOptions.First();

        Assert.Equal(subscription.Items.Data.FirstOrDefault()?.Id, passwordManagerOptions.Id);
        Assert.Equal(teamsStarterPlan.PasswordManager.StripePlanId, passwordManagerOptions.Price);
        Assert.Equal(1, passwordManagerOptions.Quantity);
        Assert.Null(passwordManagerOptions.Deleted);
    }

    [Theory]
    [BitAutoData]
    [TeamsMonthlyWithAddOnsOrganizationCustomize]
    public void RevertItemOptions_TeamsWithSMToEnterpriseWithSM_ReturnsCorrectOptions(
        Organization organization)
    {
        // 5 purchased, 1 base
        organization.MaxStorageGb = 6;

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterpriseMonthlyPlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "password_manager_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.PasswordManager.StripeSeatPlanId },
                        Quantity = organization.Seats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.SecretsManager.StripeSeatPlanId },
                        Quantity = organization.SmSeats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_service_accounts_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.SecretsManager.StripeServiceAccountPlanId },
                        Quantity = organization.SmServiceAccounts!.Value
                    },
                    new ()
                    {
                        Id = "password_manager_storage_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.PasswordManager.StripeStoragePlanId },
                        Quantity = organization.Storage!.Value
                    }
                }
            }
        };

        var updatedSubscriptionData = new SubscriptionData
        {
            Plan = enterpriseMonthlyPlan,
            PurchasedPasswordManagerSeats = 50,
            SubscribedToSecretsManager = true,
            PurchasedSecretsManagerSeats = 30,
            PurchasedAdditionalSecretsManagerServiceAccounts = 10,
            PurchasedAdditionalStorage = 10
        };

        var subscriptionUpdate = new CompleteSubscriptionUpdate(organization, updatedSubscriptionData);

        var revertItemOptions = subscriptionUpdate.RevertItemsOptions(subscription);

        Assert.Equal(4, revertItemOptions.Count);

        var passwordManagerOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.PasswordManager.StripeSeatPlanId);

        var passwordManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_subscription_item");

        Assert.Equal(passwordManagerSubscriptionItem?.Id, passwordManagerOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.PasswordManager.StripeSeatPlanId, passwordManagerOptions.Price);
        Assert.Equal(organization.Seats - teamsMonthlyPlan.PasswordManager.BaseSeats, passwordManagerOptions.Quantity);
        Assert.Null(passwordManagerOptions.Deleted);

        var secretsManagerOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.SecretsManager.StripeSeatPlanId);

        var secretsManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "secrets_manager_subscription_item");

        Assert.Equal(secretsManagerSubscriptionItem?.Id, secretsManagerOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.SecretsManager.StripeSeatPlanId, secretsManagerOptions.Price);
        Assert.Equal(organization.SmSeats - teamsMonthlyPlan.SecretsManager.BaseSeats, secretsManagerOptions.Quantity);
        Assert.Null(secretsManagerOptions.Deleted);

        var serviceAccountsOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.SecretsManager.StripeServiceAccountPlanId);

        var serviceAccountsSubscriptionItem = subscription.Items.Data.FirstOrDefault(item =>
            item.Id == "secrets_manager_service_accounts_subscription_item");

        Assert.Equal(serviceAccountsSubscriptionItem?.Id, serviceAccountsOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.SecretsManager.StripeServiceAccountPlanId, serviceAccountsOptions.Price);
        Assert.Equal(organization.SmServiceAccounts - teamsMonthlyPlan.SecretsManager.BaseServiceAccount, serviceAccountsOptions.Quantity);
        Assert.Null(serviceAccountsOptions.Deleted);

        var storageOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.PasswordManager.StripeStoragePlanId);

        var storageSubscriptionItem = subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_storage_subscription_item");

        Assert.Equal(storageSubscriptionItem?.Id, storageOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.PasswordManager.StripeStoragePlanId, storageOptions.Price);
        Assert.Equal(organization.MaxStorageGb - teamsMonthlyPlan.PasswordManager.BaseStorageGb, storageOptions.Quantity);
        Assert.Null(storageOptions.Deleted);
    }

    [Theory]
    [BitAutoData]
    [TeamsMonthlyWithAddOnsOrganizationCustomize]
    public void RevertItemOptions_TeamsWithSMToEnterpriseWithoutSM_ReturnsCorrectOptions(
        Organization organization)
    {
        // 5 purchased, 1 base
        organization.MaxStorageGb = 6;

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterpriseMonthlyPlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "password_manager_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.PasswordManager.StripeSeatPlanId },
                        Quantity = organization.Seats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.SecretsManager.StripeSeatPlanId },
                        Quantity = organization.SmSeats!.Value
                    },
                    new ()
                    {
                        Id = "secrets_manager_service_accounts_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.SecretsManager.StripeServiceAccountPlanId },
                        Quantity = organization.SmServiceAccounts!.Value
                    },
                    new ()
                    {
                        Id = "password_manager_storage_subscription_item",
                        Price = new Price { Id = enterpriseMonthlyPlan.PasswordManager.StripeStoragePlanId },
                        Quantity = organization.Storage!.Value
                    }
                }
            }
        };

        var updatedSubscriptionData = new SubscriptionData
        {
            Plan = enterpriseMonthlyPlan,
            PurchasedPasswordManagerSeats = 50,
            SubscribedToSecretsManager = false,
            PurchasedSecretsManagerSeats = 0,
            PurchasedAdditionalSecretsManagerServiceAccounts = 0,
            PurchasedAdditionalStorage = 10
        };

        var subscriptionUpdate = new CompleteSubscriptionUpdate(organization, updatedSubscriptionData);

        var revertItemOptions = subscriptionUpdate.RevertItemsOptions(subscription);

        Assert.Equal(4, revertItemOptions.Count);

        var passwordManagerOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.PasswordManager.StripeSeatPlanId);

        var passwordManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_subscription_item");

        Assert.Equal(passwordManagerSubscriptionItem?.Id, passwordManagerOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.PasswordManager.StripeSeatPlanId, passwordManagerOptions.Price);
        Assert.Equal(organization.Seats - teamsMonthlyPlan.PasswordManager.BaseSeats, passwordManagerOptions.Quantity);
        Assert.Null(passwordManagerOptions.Deleted);

        var secretsManagerOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.SecretsManager.StripeSeatPlanId);

        var secretsManagerSubscriptionItem =
            subscription.Items.Data.FirstOrDefault(item => item.Id == "secrets_manager_subscription_item");

        Assert.Equal(secretsManagerSubscriptionItem?.Id, secretsManagerOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.SecretsManager.StripeSeatPlanId, secretsManagerOptions.Price);
        Assert.Equal(organization.SmSeats - teamsMonthlyPlan.SecretsManager.BaseSeats, secretsManagerOptions.Quantity);
        Assert.Null(secretsManagerOptions.Deleted);

        var serviceAccountsOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.SecretsManager.StripeServiceAccountPlanId);

        var serviceAccountsSubscriptionItem = subscription.Items.Data.FirstOrDefault(item =>
            item.Id == "secrets_manager_service_accounts_subscription_item");

        Assert.Equal(serviceAccountsSubscriptionItem?.Id, serviceAccountsOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.SecretsManager.StripeServiceAccountPlanId, serviceAccountsOptions.Price);
        Assert.Equal(organization.SmServiceAccounts - teamsMonthlyPlan.SecretsManager.BaseServiceAccount, serviceAccountsOptions.Quantity);
        Assert.Null(serviceAccountsOptions.Deleted);

        var storageOptions = revertItemOptions.FirstOrDefault(options =>
            options.Price == teamsMonthlyPlan.PasswordManager.StripeStoragePlanId);

        var storageSubscriptionItem = subscription.Items.Data.FirstOrDefault(item => item.Id == "password_manager_storage_subscription_item");

        Assert.Equal(storageSubscriptionItem?.Id, storageOptions!.Id);
        Assert.Equal(teamsMonthlyPlan.PasswordManager.StripeStoragePlanId, storageOptions.Price);
        Assert.Equal(organization.MaxStorageGb - teamsMonthlyPlan.PasswordManager.BaseStorageGb, storageOptions.Quantity);
        Assert.Null(storageOptions.Deleted);
    }
}
