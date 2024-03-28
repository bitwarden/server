CREATE TABLE [dbo].[FCBackupAccessAllOrganizationUsers] (
    [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationUserId]    UNIQUEIDENTIFIER    NOT NULL,
    PRIMARY KEY CLUSTERED ([OrganizationId], [OrganizationUserId])
);
