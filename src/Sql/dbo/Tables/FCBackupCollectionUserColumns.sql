CREATE TABLE [dbo].[FCBackupCollectionUserColumns] (
    [OrganizationId]       UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]         UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationUserId]   UNIQUEIDENTIFIER    NOT NULL,
    [ReadOnly]             BIT                 NOT NULL,
    [HidePasswords]        BIT                 NOT NULL,
    PRIMARY KEY CLUSTERED ([OrganizationId], [CollectionId], [OrganizationUserId])
);
