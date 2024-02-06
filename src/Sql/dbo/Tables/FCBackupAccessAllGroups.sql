CREATE TABLE [dbo].[FCBackupAccessAllGroups] (
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [GroupId]           UNIQUEIDENTIFIER    NOT NULL
    PRIMARY KEY CLUSTERED ([OrganizationId], [GroupId])
);
