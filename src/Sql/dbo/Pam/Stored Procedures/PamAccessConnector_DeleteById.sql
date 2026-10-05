CREATE PROCEDURE [dbo].[PamAccessConnector_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Cascade in one transaction: NO ACTION FKs mean assignments, then access connector, then credential.
    SET XACT_ABORT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()
    DECLARE @ApiKeyId UNIQUEIDENTIFIER

    BEGIN TRANSACTION

    -- The stored row decides which credential goes; the caller's ApiKeyId is not trusted.
    SELECT @ApiKeyId = [ApiKeyId]
    FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    -- No FK ties PamRotationJob to PamAccessConnector, so this clears the access connector's claimed jobs directly.
    UPDATE AT
    SET AT.[Status] = 3, -- Abandoned
        AT.[ResolvedDate] = @Now
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AT.[JobId]
    WHERE AT.[Status] = 0 -- Executing
        AND J.[ClaimedByAccessConnectorId] = @Id
        AND J.[Status] = 1 -- Claimed

    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 0, -- Pending
        [ClaimedByAccessConnectorId] = NULL,
        [ClaimedAt] = NULL,
        [NextClaimableAt] = @Now
    WHERE [ClaimedByAccessConnectorId] = @Id
        AND [Status] = 1 -- Claimed

    DELETE FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @Id

    DELETE FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    DELETE FROM [dbo].[ApiKey]
    WHERE [Id] = @ApiKeyId

    COMMIT TRANSACTION
END
