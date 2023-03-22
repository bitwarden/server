﻿using Bit.Admin.Enums;

namespace Bit.Admin.Utilities;

public static class RolePermissionMapping
{
    //This is temporary and will be moved to the db in the next round of the rbac implementation
    public static readonly Dictionary<string, List<Permission>> RolePermissions = new Dictionary<string, List<Permission>>()
    {
        { "owner", new List<Permission>
            {
                Permission.User_List_View,
                Permission.User_UserInformation_View,
                Permission.User_GeneralDetails_View,
                Permission.User_Delete,
                Permission.User_UpgradePremium,
                Permission.User_BillingInformation_View,
                Permission.User_BillingInformation_DownloadInvoice,
                Permission.User_Premium_View,
                Permission.User_Premium_Edit,
                Permission.User_Licensing_View,
                Permission.User_Licensing_Edit,
                Permission.User_Billing_View,
                Permission.User_Billing_Edit,
                Permission.User_Billing_LaunchGateway,
                Permission.Org_List_View,
                Permission.Org_OrgInformation_View,
                Permission.Org_GeneralDetails_View,
                Permission.Org_BusinessInformation_View,
                Permission.Org_InitiateTrial,
                Permission.Org_Delete,
                Permission.Org_BillingInformation_View,
                Permission.Org_BillingInformation_DownloadInvoice,
                Permission.Org_Plan_View,
                Permission.Org_Plan_Edit,
                Permission.Org_Licensing_View,
                Permission.Org_Licensing_Edit,
                Permission.Org_Billing_View,
                Permission.Org_Billing_Edit,
                Permission.Org_Billing_LaunchIDInStripe,
                Permission.Provider_List_View,
                Permission.Provider_Create,
                Permission.Provider_View,
                Permission.Provider_ResendEmailInvite,
                Permission.Tools_ChargeBrainTreeCustomer,
                Permission.Tools_PromoteAdmin,
                Permission.Tools_GenerateLicenseFile,
                Permission.Tools_ManageTaxRates,
                Permission.Tools_ManageStripeSubscriptions,
                Permission.Logs_View
            }
        },
        { "admin", new List<Permission>
            {
                Permission.User_List_View,
                Permission.User_UserInformation_View,
                Permission.User_GeneralDetails_View,
                Permission.User_Delete,
                Permission.User_UpgradePremium,
                Permission.User_BillingInformation_View,
                Permission.User_BillingInformation_DownloadInvoice,
                Permission.User_Premium_View,
                Permission.User_Premium_Edit,
                Permission.User_Licensing_View,
                Permission.User_Licensing_Edit,
                Permission.User_Billing_View,
                Permission.User_Billing_Edit,
                Permission.User_Billing_LaunchGateway,
                Permission.Org_List_View,
                Permission.Org_OrgInformation_View,
                Permission.Org_GeneralDetails_View,
                Permission.Org_BusinessInformation_View,
                Permission.Org_Delete,
                Permission.Org_BillingInformation_View,
                Permission.Org_BillingInformation_DownloadInvoice,
                Permission.Org_Plan_View,
                Permission.Org_Plan_Edit,
                Permission.Org_Licensing_View,
                Permission.Org_Licensing_Edit,
                Permission.Org_Billing_View,
                Permission.Org_Billing_Edit,
                Permission.Org_Billing_LaunchIDInStripe,
                Permission.Org_InitiateTrial,
                Permission.Provider_List_View,
                Permission.Provider_Create,
                Permission.Provider_View,
                Permission.Provider_ResendEmailInvite,
                Permission.Tools_ChargeBrainTreeCustomer,
                Permission.Tools_PromoteAdmin,
                Permission.Tools_GenerateLicenseFile,
                Permission.Tools_ManageTaxRates,
                Permission.Tools_ManageStripeSubscriptions,
                Permission.Logs_View
            }
        },
        { "cs", new List<Permission>
            {
                Permission.User_List_View,
                Permission.User_UserInformation_View,
                Permission.User_GeneralDetails_View,
                Permission.User_UpgradePremium,
                Permission.User_BillingInformation_View,
                Permission.User_BillingInformation_DownloadInvoice,
                Permission.User_Premium_View,
                Permission.User_Licensing_View,
                Permission.User_Billing_View,
                Permission.User_Billing_LaunchGateway,
                Permission.Org_List_View,
                Permission.Org_OrgInformation_View,
                Permission.Org_GeneralDetails_View,
                Permission.Org_BusinessInformation_View,
                Permission.Org_BillingInformation_View,
                Permission.Org_BillingInformation_DownloadInvoice,
                Permission.Org_Plan_View,
                Permission.Org_Licensing_View,
                Permission.Org_Billing_View,
                Permission.Org_Billing_LaunchIDInStripe,
                Permission.Provider_List_View,
                Permission.Provider_View,
                Permission.Logs_View
            }
        },
        { "billing", new List<Permission>
            {
                Permission.User_List_View,
                Permission.User_UserInformation_View,
                Permission.User_GeneralDetails_View,
                Permission.User_UpgradePremium,
                Permission.User_BillingInformation_View,
                Permission.User_BillingInformation_DownloadInvoice,
                Permission.User_BillingInformation_CreateEditTransaction,
                Permission.User_Premium_View,
                Permission.User_Licensing_View,
                Permission.User_Billing_View,
                Permission.User_Billing_Edit,
                Permission.User_Billing_LaunchGateway,
                Permission.Org_List_View,
                Permission.Org_OrgInformation_View,
                Permission.Org_GeneralDetails_View,
                Permission.Org_BusinessInformation_View,
                Permission.Org_BillingInformation_View,
                Permission.Org_BillingInformation_DownloadInvoice,
                Permission.Org_BillingInformation_CreateEditTransaction,
                Permission.Org_Plan_View,
                Permission.Org_Plan_Edit,
                Permission.Org_Licensing_View,
                Permission.Org_Billing_View,
                Permission.Org_Billing_Edit,
                Permission.Org_Billing_LaunchIDInStripe,
                Permission.Provider_Edit,
                Permission.Provider_View,
                Permission.Tools_ChargeBrainTreeCustomer,
                Permission.Tools_GenerateLicenseFile,
                Permission.Tools_ManageTaxRates,
                Permission.Tools_ManageStripeSubscriptions,
                Permission.Tools_CreateEditTransaction,
                Permission.Logs_View
            }
        },
        { "sales", new List<Permission>
            {
                Permission.User_List_View,
                Permission.User_UserInformation_View,
                Permission.User_GeneralDetails_View,
                Permission.User_BillingInformation_View,
                Permission.User_BillingInformation_DownloadInvoice,
                Permission.User_Premium_View,
                Permission.User_Licensing_View,
                Permission.User_Licensing_Edit,
                Permission.Org_List_View,
                Permission.Org_OrgInformation_View,
                Permission.Org_GeneralDetails_View,
                Permission.Org_BusinessInformation_View,
                Permission.Org_InitiateTrial,
                Permission.Org_BillingInformation_View,
                Permission.Org_BillingInformation_DownloadInvoice,
                Permission.Org_Licensing_View,
                Permission.Org_Licensing_Edit,
                Permission.Provider_List_View,
                Permission.Provider_Create,
                Permission.Provider_Edit,
                Permission.Provider_View,
                Permission.Provider_ResendEmailInvite,
                Permission.Logs_View
            }
        },
    };
}
