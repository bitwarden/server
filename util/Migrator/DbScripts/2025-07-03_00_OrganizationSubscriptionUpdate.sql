IF OBJECT_ID('dbo.OrganizationSubscriptionUpdate') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrganizationSubscriptionUpdate]
        (
            [Id]               UNIQUEIDENTIFIER NOT NULL,
            [OrganizationId]   UNIQUEIDENTIFIER NOT NULL,
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

CREATE OR ALTER VIEW [dbo].[OrganizationSubscriptionUpdateView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationSubscriptionUpdate]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSubscriptionUpdate_SetToUpdateSubscription]
    @OrganizationId UNIQUEIDENTIFIER,
    @SeatsLastUpdated DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    IF EXISTS (SELECT 1 FROM [dbo].[OrganizationSubscriptionUpdate] WHERE [OrganizationId] = @OrganizationId)
        UPDATE
            [dbo].[OrganizationSubscriptionUpdate]
        SET
            [SeatsLastUpdated] = @SeatsLastUpdated,
            [SyncAttempts] = 0
        WHERE
            [OrganizationId] = @OrganizationId
    ELSE
        INSERT INTO [dbo].[OrganizationSubscriptionUpdate] (Id, OrganizationId, SeatsLastUpdated, SyncAttempts)
        VALUES (NEWID(), @OrganizationId, @SeatsLastUpdated, 0)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSubscriptionUpdate_GetUpdatesToSubscription]
AS
BEGIN
    SELECT *
    FROM [dbo].[OrganizationSubscriptionUpdateView]
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

    ;WITH OrgActions AS (
        -- Failed orgs take precedence
        SELECT Id, 'Failed' AS Action FROM @FailedOrgIds
        UNION ALL
        -- Successful orgs only if not in failed list
        SELECT Id, 'Successful' AS Action FROM @SuccessfulOrgIds
        WHERE Id NOT IN (SELECT Id FROM @FailedOrgIds)
    )
     UPDATE osu
     SET
         [SeatsLastUpdated] = CASE
                                  WHEN oa.Action = 'Successful' THEN NULL
                                  ELSE osu.[SeatsLastUpdated]
             END,
         [SyncAttempts] = CASE
                              WHEN oa.Action = 'Failed' THEN osu.[SyncAttempts] + 1
                              WHEN oa.Action = 'Successful' THEN 0
             END
     FROM [dbo].[OrganizationSubscriptionUpdate] osu
              INNER JOIN OrgActions oa ON osu.OrganizationId = oa.Id
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
    WHERE [Id] = @OrganizationId
END
GO
