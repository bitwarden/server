CREATE PROCEDURE [dbo].[PamRotationAttempt_UpdateCipherData]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- The job's UPDLOCK holds the claim, and the revision check sits in the cipher UPDATE, so neither can change
    -- between check and write.
    SET XACT_ABORT ON

    DECLARE @CipherId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @VerifiedJobId UNIQUEIDENTIFIER

    BEGIN TRY
        BEGIN TRANSACTION

        SELECT
            @CipherId = C.[CipherId],
            @OrganizationId = C.[OrganizationId],
            @VerifiedJobId = J.[Id]
        FROM
            [dbo].[PamRotationAttempt] AT
        INNER JOIN
            [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
        INNER JOIN
            [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
        WHERE
            AT.[Id] = @AttemptId
            AND AT.[Action] = 0 -- None
            AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
            AND J.[Action] = 1 -- Claimed
            AND J.[ExpirationDate] > @Now
            AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
            AND J.[ClaimedDate] = AT.[CreationDate]

        IF @VerifiedJobId IS NULL
        BEGIN
            -- Unknown attempt, wrong claimant, or an already-resolved job/attempt; caller audits as write_rejected.
            ROLLBACK TRANSACTION
            SELECT 0 -- Rejected
            RETURN
        END

        -- Drifted LastKnownRevisionDate means a concurrent edit; rejected, matching CipherService's tolerance. The
        -- check is part of the UPDATE, so a save can't land between it and the write.
        UPDATE
            [dbo].[Cipher]
        SET
            [Data] = @CipherData,
            [RevisionDate] = @Now
        WHERE
            [Id] = @CipherId
            AND ABS(DATEDIFF_BIG(MILLISECOND, [RevisionDate], @LastKnownRevisionDate)) <= 1000

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION

            -- The config has no FK to Cipher, so the cipher may have been hard-deleted.
            IF EXISTS (
                SELECT
                    1
                FROM
                    [dbo].[Cipher]
                WHERE
                    [Id] = @CipherId
            )
            BEGIN
                SELECT -1 -- RevisionMismatch
            END
            ELSE
            BEGIN
                SELECT 0 -- Rejected
            END

            RETURN
        END

        UPDATE
            [dbo].[PamRotationAttempt]
        SET
            [CipherUpdated] = 1
        WHERE
            [Id] = @AttemptId

        -- Other writers of dbo.Cipher bump here too, so clients do not keep a stale password.
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @CipherId, @OrganizationId

        COMMIT TRANSACTION

        SELECT 1 -- Accepted
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
