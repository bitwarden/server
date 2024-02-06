CREATE TABLE [dbo].[FCBackupOrganizationUserManagers] (
    [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationUserId]    UNIQUEIDENTIFIER    NOT NULL,
    [Type]                  INT                 NOT NULL,
    PRIMARY KEY CLUSTERED ([OrganizationId], [OrganizationUserId])
);
