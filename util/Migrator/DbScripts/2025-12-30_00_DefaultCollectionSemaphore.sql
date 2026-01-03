-- Create DefaultCollectionSemaphore table
-- Cascade behavior: Organization -> OrganizationUser (CASCADE) -> DefaultCollectionSemaphore (CASCADE)
-- OrganizationId FK has NO ACTION to avoid competing cascade paths
IF OBJECT_ID('[dbo].[DefaultCollectionSemaphore]') IS NULL
BEGIN
    CREATE TABLE [dbo].[DefaultCollectionSemaphore]
    (
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreationDate]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_DefaultCollectionSemaphore] PRIMARY KEY CLUSTERED ([OrganizationUserId] ASC),
    CONSTRAINT [FK_DefaultCollectionSemaphore_OrganizationUser] FOREIGN KEY ([OrganizationUserId])
        REFERENCES [dbo].[OrganizationUser] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Create stored procedure to read semaphores by OrganizationUserId
CREATE OR ALTER PROCEDURE [dbo].[DefaultCollectionSemaphore_ReadByOrganizationUserIds]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId]
    FROM
        [dbo].[DefaultCollectionSemaphore] DCS
    INNER JOIN
        @OrganizationUserIds OU ON [OU].[Id] = [DCS].[OrganizationUserId]
END
GO
