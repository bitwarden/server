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

    SELECT @ApiKeyId = [ApiKeyId]
    FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    -- No FK ties PamRotationJob to PamAccessConnector, so this releases the access connector's live claims
    -- directly. Their attempts derive as Abandoned; only the end is recorded. A timed-out claim is left as it was.
    UPDATE AT
    SET AT.[ResolvedDate] = @Now
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AT.[JobId]
    WHERE AT.[Action] = 0 -- None
        AND AT.[ResolvedDate] IS NULL
        AND J.[ClaimedByAccessConnectorId] = @Id
        AND J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now

    UPDATE [dbo].[PamRotationJob]
    SET [Action] = 0, -- None
        [ClaimedByAccessConnectorId] = NULL,
        [ClaimedAt] = NULL,
        [NextClaimableAt] = @Now
    WHERE [ClaimedByAccessConnectorId] = @Id
        AND [Action] = 1 -- Claimed
        AND [ExpiresAt] > @Now

    DELETE FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @Id

    DELETE FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    DELETE FROM [dbo].[ApiKey]
    WHERE [Id] = @ApiKeyId

    COMMIT TRANSACTION
END
