CREATE TABLE [dbo].[FCBackupOrganizationUserManagers] (
    [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationUserId]    UNIQUEIDENTIFIER    NOT NULL,
    PRIMARY KEY CLUSTERED ([OrganizationId], [OrganizationUserId])
);
