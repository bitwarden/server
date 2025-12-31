-- Semaphore table to prevent duplicate default collections per organization user
-- Cascade behavior: Organization -> OrganizationUser (CASCADE) -> DefaultCollectionSemaphore (CASCADE)
-- OrganizationId FK has NO ACTION to avoid competing cascade paths
CREATE TABLE [dbo].[DefaultCollectionSemaphore]
(
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreationDate]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_DefaultCollectionSemaphore] PRIMARY KEY CLUSTERED ([OrganizationUserId] ASC),
    CONSTRAINT [FK_DefaultCollectionSemaphore_OrganizationUser] FOREIGN KEY ([OrganizationUserId])
        REFERENCES [dbo].[OrganizationUser] ([Id]) ON DELETE CASCADE
);
