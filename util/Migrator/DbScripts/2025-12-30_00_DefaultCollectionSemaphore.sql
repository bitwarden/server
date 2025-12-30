-- Create DefaultCollectionSemaphore table
-- Cascade behavior: Organization -> OrganizationUser (CASCADE) -> DefaultCollectionSemaphore (CASCADE)
-- OrganizationId FK has NO ACTION to avoid competing cascade paths
IF OBJECT_ID('[dbo].[DefaultCollectionSemaphore]') IS NULL
BEGIN
    CREATE TABLE [dbo].[DefaultCollectionSemaphore]
    (
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
        [CreationDate] DATETIME2(7) NOT NULL,
        CONSTRAINT [PK_DefaultCollectionSemaphore] PRIMARY KEY CLUSTERED
        (
            [OrganizationId] ASC,
            [OrganizationUserId] ASC
        ),
        CONSTRAINT [FK_DefaultCollectionSemaphore_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]), -- NO ACTION to avoid competing cascades
        CONSTRAINT [FK_DefaultCollectionSemaphore_OrganizationUser] FOREIGN KEY ([OrganizationUserId])
            REFERENCES [dbo].[OrganizationUser] ([Id])
            ON DELETE CASCADE -- Cascades from OrganizationUser deletion
    );
END
GO
