CREATE PROCEDURE [dbo].[PamRotationAttempt_UpdateCipherData]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- The job's UPDLOCK holds the claim, and Cipher_UpdateData checks the revision in its UPDATE, so neither can
    -- change between check and write.
    SET XACT_ABORT ON

    DECLARE @CipherId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @VerifiedJobId UNIQUEIDENTIFIER
    DECLARE @CipherOutcome INT

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

        -- Joins this transaction, and bumps the account revision dates on success.
        EXEC [dbo].[Cipher_UpdateData]
            @Id = @CipherId,
            @OrganizationId = @OrganizationId,
            @Data = @CipherData,
            @RevisionDate = @Now,
            @LastKnownRevisionDate = @LastKnownRevisionDate,
            @Outcome = @CipherOutcome OUTPUT

        IF @CipherOutcome <> 1 -- Updated
        BEGIN
            ROLLBACK TRANSACTION
            SELECT
                CASE @CipherOutcome
                    WHEN -1 THEN -1 -- RevisionMismatch: a concurrent edit moved the revision date
                    ELSE 0 -- Rejected: the cipher was hard-deleted, as the config has no FK to Cipher
                END
            RETURN
        END

        UPDATE
            [dbo].[PamRotationAttempt]
        SET
            [CipherUpdated] = 1
        WHERE
            [Id] = @AttemptId

        COMMIT TRANSACTION

        SELECT 1 -- Accepted
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
