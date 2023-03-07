namespace Bit.Admin.Enums;

public enum Permission
{
    User_List_View,
    User_UserInformation_View,
    User_GeneralDetails_View,
    User_Delete,
    User_UpgradePremium,
    User_BillingInformation_View,
    User_BillingInformation_DownloadInvoice,
    User_BillingInformation_CreateEditTransaction,
    User_Premium_View,
    User_Premium_Edit,
    User_Licensing_View,
    User_Licensing_Edit,
    User_Billing_View,
    User_Billing_Edit,
    User_Billing_LaunchGateway,

    Org_List_View,
    Org_OrgInformation_View,
    Org_GeneralDetails_View,
    Org_BusinessInformation_View,
    Org_InitiateTrial,
    Org_Delete,
    Org_BillingInformation_View,
    Org_BillingInformation_DownloadInvoice,
    Org_BillingInformation_CreateEditTransaction,
    Org_Plan_View,
    Org_Plan_Edit,
    Org_Licensing_View,
    Org_Licensing_Edit,
    Org_Billing_View,
    Org_Billing_Edit,
    Org_Billing_LaunchIDInStripe,

    Provider_ViewList,
    Provider_Create,
    Provider_View,

    Tools_ChargeBrainTreeCustomer,
    Tools_PromoteAdmin,
    Tools_GenerateLicenseFile,
    Tools_ManageTaxRates,
    Tools_ManageStripeSubscriptions,
    Tools_CreateEditTransaction,

    Logs_View
}
