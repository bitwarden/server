using Bit.Admin.Components.Navigation;
using Bit.Admin.Enums;
using Microsoft.AspNetCore.Components;

namespace Bit.Admin.AdminConsole.Components;

public partial class App : ComponentBase
{
    public HashSet<NavItem.ViewModel> NavItems { get; private set; }

    protected override void OnInitialized()
    {
        var canViewUsers = AccessControlService.UserHasPermission(Permission.User_List_View);
        var canViewOrgs = AccessControlService.UserHasPermission(Permission.Org_List_View);
        var canViewProviders = AccessControlService.UserHasPermission(Permission.Provider_List_View);
        var canChargeBraintree = AccessControlService.UserHasPermission(Permission.Tools_ChargeBrainTreeCustomer);
        var canCreateTransaction = AccessControlService.UserHasPermission(Permission.Tools_CreateEditTransaction);
        var canPromoteAdmin = AccessControlService.UserHasPermission(Permission.Tools_PromoteAdmin);
        var canGenerateLicense = AccessControlService.UserHasPermission(Permission.Tools_GenerateLicenseFile);
        var canManageTaxRates = AccessControlService.UserHasPermission(Permission.Tools_ManageTaxRates);
        var canManageStripeSubscriptions = AccessControlService.UserHasPermission(Permission.Tools_ManageStripeSubscriptions);
        var canProcessStripeEvents = AccessControlService.UserHasPermission(Permission.Tools_ProcessStripeEvents);
        var canMigrateProviders = AccessControlService.UserHasPermission(Permission.Tools_MigrateProviders);

        var canViewTools = canChargeBraintree || canCreateTransaction || canPromoteAdmin ||
                           canGenerateLicense || canManageTaxRates || canManageStripeSubscriptions;

        NavItems =
        [
            new() { Label = "Users", Link = "/users", Show = canViewUsers },

            new() { Label = "Organizations", Link = "/organizations", Show = canViewOrgs },
            new() { Label = "Providers", Link = "/providers", Show = canViewProviders && !GlobalSettings.SelfHosted },
            new()
            {
                Label = "Tools",
                Link = "/tools",
                Show = canViewTools && !GlobalSettings.SelfHosted,
                DropDownItems =
                [
                    new()
                    {
                        Label = "Charge Braintree Customer",
                        Link = "/tools/chargebraintree",
                        Show = canChargeBraintree
                    },

                    new()
                    {
                        Label = "Create/Edit Transaction",
                        Link = "/tools/createtransaction",
                        Show = canCreateTransaction
                    },

                    new() { Label = "Promote Admin", Link = "/tools/promoteadmin", Show = canPromoteAdmin },
                    new()
                    {
                        Label = "Generate License File",
                        Link = "/tools/generatelicense",
                        Show = canGenerateLicense
                    },

                    new() { Label = "Manage Tax Rates", Link = "/tools/taxrate", Show = canManageTaxRates },

                    new()
                    {
                        Label = "Manage Stripe Subscriptions",
                        Link = "/tools/stripesubscriptions",
                        Show = canManageStripeSubscriptions
                    },

                    new()
                    {
                        Label = "Process Stripe Events",
                        Link = "/process-stripe-events",
                        Show = canProcessStripeEvents
                    },

                    new() { Label = "Migrate Providers", Link = "/tools/migrateproviders", Show = canMigrateProviders }
                ]
            }
        ];
    }
}
