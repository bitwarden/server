namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class AccessEventLogsRequirement() : BasePermissionRequirement(p => p.AccessEventLogs);
public class AccessImportExportRequirement() : BasePermissionRequirement(p => p.AccessImportExport);
public class AccessReportsRequirement() : BasePermissionRequirement(p => p.AccessReports);
public class ManageAccountRecoveryRequirement() : BasePermissionRequirement(p => p.ManageResetPassword);
public class ManageGroupsRequirement() : BasePermissionRequirement(p => p.ManageGroups);
public class ManagePoliciesRequirement() : BasePermissionRequirement(p => p.ManagePolicies);
public class ManageScimRequirement() : BasePermissionRequirement(p => p.ManageScim);
public class ManageSsoRequirement() : BasePermissionRequirement(p => p.ManageSso);
public class ManageUsersRequirement() : BasePermissionRequirement(p => p.ManageUsers);
