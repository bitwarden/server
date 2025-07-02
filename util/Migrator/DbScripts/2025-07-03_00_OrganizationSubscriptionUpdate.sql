IF OBJECT_ID('dbo.OrganizationSubscriptionUpdate') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrganizationSubscriptionUpdate]
        (
            [Id]               UniqueIdentifier NOT NULL,
            [OrganizationId]   UniqueIdentifier NOT NULL,
            [SeatsLastUpdated] DATETIME2        NULL,
            [SyncAttempts]     INT              NOT NULL DEFAULT (0),
            CONSTRAINT [PK_OrganizationSubscriptionUpdate] PRIMARY KEY CLUSTERED ([Id] ASC),
            CONSTRAINT [FK_OrganizationSubscriptionUpdate_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
        )

        CREATE NONCLUSTERED INDEX [IX_OrganizationSubscriptionUpdate_SeatsLastUpdated]
            ON [dbo].[OrganizationSubscriptionUpdate] ([SeatsLastUpdated] ASC)
            INCLUDE ([OrganizationId]);
    END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSubscriptionUpdate_SetToUpdateSubscription]
    @OrganizationId UNIQUEIDENTIFIER,
    @SeatsLastUpdated DATETIME2,
    @SyncAttempts INT
AS
BEGIN
    SET NOCOUNT ON

    IF EXISTS (SELECT 1 FROM [dbo].[OrganizationSubscriptionUpdate] WHERE [OrganizationId] = @OrganizationId)
        UPDATE
            [dbo].[OrganizationSubscriptionUpdate]
        SET
            [SeatsLastUpdated] = @SeatsLastUpdated,
            [SyncAttempts] = @SyncAttempts
        WHERE
            [OrganizationId] = @OrganizationId
    ELSE
        INSERT INTO [dbo].[OrganizationSubscriptionUpdate] (Id, OrganizationId, SeatsLastUpdated, SyncAttempts)
        VALUES (NEWID(), @OrganizationId, @SeatsLastUpdated, @SyncAttempts)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSubscriptionUpdate_GetUpdatesToSubscription]
AS
BEGIN
    SELECT *
    FROM [dbo].[OrganizationSubscriptionUpdate]
    WHERE [SeatsLastUpdated] IS NOT NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSubscriptionUpdate_UpdateSubscriptionStatus]
    @SuccessfulOrganizations NVARCHAR(MAX),
    @FailedOrganizations NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @SuccessfulOrgIds TABLE (Id UNIQUEIDENTIFIER)
    DECLARE @FailedOrgIds TABLE (Id UNIQUEIDENTIFIER)

    INSERT INTO @SuccessfulOrgIds (Id)
    SELECT [value] FROM OPENJSON(@SuccessfulOrganizations)

    INSERT INTO @FailedOrgIds (Id)
    SELECT [value] FROM OPENJSON(@FailedOrganizations)

    UPDATE [dbo].[OrganizationSubscriptionUpdate]
    SET
        [SeatsLastUpdated] = NULL,
        [SyncAttempts] = 0
    WHERE [OrganizationId] IN (SELECT Id FROM @SuccessfulOrgIds)

    UPDATE [dbo].[OrganizationSubscriptionUpdate]
    SET
        [SyncAttempts] = [SyncAttempts] + 1
    WHERE [OrganizationId] IN (SELECT Id FROM @FailedOrgIds)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_IncrementSeatCount]
    @OrganizationId UNIQUEIDENTIFIER,
    @SeatsToAdd INT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[Organization]
    SET [Seats] = [Seats] + @SeatsToAdd
    WHERE Id = @OrganizationId
END
GO
