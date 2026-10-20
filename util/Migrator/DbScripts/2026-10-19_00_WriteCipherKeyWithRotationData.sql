-- Writes the cipher's key together with its data when a rotation write-back carries one: the data is encrypted under
-- that key, so a data-only write would leave a key that no longer matches. The new parameters default to leaving the
-- key alone, so a server that predates them keeps working during a rolling deployment.
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @LastKnownRevisionDate DATETIME2(7),
    @Outcome INT OUTPUT,
    @UpdateKey BIT = 0,
    @Key VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Opens no transaction, so it joins the caller's; a rollback here would undo the caller's work too.
    -- The revision check sits in the UPDATE, so the row lock covers check and write. A separate read would take no
    -- lock under RCSI, letting a user's save land in between and be overwritten. The 1-second tolerance matches
    -- CipherService.
    -- The key is written with the data because the data is encrypted under it: a data-only write would leave a key
    -- that no longer matches. @UpdateKey defaults to 0 so a caller that predates it keeps the stored key, since a
    -- NULL @Key is also a valid new value (an item encrypted directly under the organization key).
    UPDATE
        [dbo].[Cipher]
    SET
        [Data] = @Data,
        [Key] = CASE WHEN @UpdateKey = 1 THEN @Key ELSE [Key] END,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND ABS(DATEDIFF_BIG(MILLISECOND, [RevisionDate], @LastKnownRevisionDate)) <= 1000

    IF @@ROWCOUNT = 0
    BEGIN
        -- 0 = deleted, so retrying can't help; -1 = changed by someone else, so retry from a fresh revision date.
        SET @Outcome = CASE
            WHEN EXISTS (
                SELECT
                    1
                FROM
                    [dbo].[Cipher]
                WHERE
                    [Id] = @Id
            ) THEN -1
            ELSE 0
        END
        RETURN
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId

    SET @Outcome = 1 -- Updated
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_UpdateCipherData]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7),
    @UpdateCipherKey BIT = 0,
    @CipherKey VARCHAR(MAX) = NULL
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
            @Outcome = @CipherOutcome OUTPUT,
            @UpdateKey = @UpdateCipherKey,
            @Key = @CipherKey

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
GO
