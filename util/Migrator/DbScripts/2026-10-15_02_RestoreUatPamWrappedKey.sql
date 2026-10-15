-- Reinstalls pam/uat's WrappedKey-aware rotation view and procedures. Main's 2026-10-12_00_AddPamRotation.sql
-- replaces them with versions that don't know the column when it first runs on a UAT database that already ran
-- 2026-10-15_01_ReshapeUatPamRotation.sql. Drop this once the wrapped key ships to main.
GO

-- Denormalizes the target's name/method and computes HasActiveJob, so list and header reads take one round trip.
-- A timed-out job stays active until the timeout sweep records it.
CREATE OR ALTER VIEW [dbo].[PamRotationConfigDetailsView]
AS
SELECT
    C.[Id],
    C.[OrganizationId],
    C.[CipherId],
    C.[TargetSystemId],
    C.[ScheduleCron],
    C.[RotateOnAccessEnd],
    C.[NextRotationDate],
    C.[Enabled],
    C.[LastRotationDate],
    C.[CreationDate],
    C.[RevisionDate],
    C.[SealedData],
    C.[WrappedKey],
    T.[Name] AS [TargetSystemName],
    T.[Method] AS [TargetSystemMethod],
    CASE
        WHEN EXISTS (
            SELECT
                1
            FROM
                [dbo].[PamRotationJob] J
            WHERE
                J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0,1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT
                        1
                    FROM
                        [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE
                        S.[RotationJobId] = J.[Id]
                )
        ) THEN 1
        ELSE 0
    END AS [HasActiveJob]
FROM
    [dbo].[PamRotationConfig] C
INNER JOIN
    [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
    @WrappedKey NVARCHAR(MAX),
    @ScheduleCron NVARCHAR(100) = NULL,
    @RotateOnAccessEnd BIT,
    @NextRotationDate DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationDate DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamRotationConfig]
    (
        [Id],
        [OrganizationId],
        [CipherId],
        [TargetSystemId],
        [SealedData],
        [WrappedKey],
        [ScheduleCron],
        [RotateOnAccessEnd],
        [NextRotationDate],
        [Enabled],
        [LastRotationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CipherId,
        @TargetSystemId,
        @SealedData,
        @WrappedKey,
        @ScheduleCron,
        @RotateOnAccessEnd,
        @NextRotationDate,
        @Enabled,
        @LastRotationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
    @WrappedKey NVARCHAR(MAX),
    @ScheduleCron NVARCHAR(100) = NULL,
    @RotateOnAccessEnd BIT,
    @NextRotationDate DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationDate DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[PamRotationConfig]
    SET
        [OrganizationId] = @OrganizationId,
        [CipherId] = @CipherId,
        [TargetSystemId] = @TargetSystemId,
        [SealedData] = @SealedData,
        [WrappedKey] = @WrappedKey,
        [ScheduleCron] = @ScheduleCron,
        [RotateOnAccessEnd] = @RotateOnAccessEnd,
        [NextRotationDate] = @NextRotationDate,
        [Enabled] = @Enabled,
        [LastRotationDate] = @LastRotationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_UpdateClaim]
    @JobId UNIQUEIDENTIFIER,
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- First-claim-wins: the UPDATE's WHERE Action = 0 takes the row lock, serializing concurrent claims.
    SET XACT_ABORT ON

    DECLARE @Outcome INT

    BEGIN TRY
        BEGIN TRANSACTION

        UPDATE
            J
        SET
            J.[Action] = 1, -- Claimed
            J.[ClaimedByAccessConnectorId] = @AccessConnectorId,
            J.[ClaimedDate] = @Now
        FROM
            [dbo].[PamRotationJob] J
        INNER JOIN
            [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
        INNER JOIN
            [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
        INNER JOIN
            [dbo].[PamAccessConnectorTargetAssignment] A ON A.[AccessConnectorId] = @AccessConnectorId
                AND A.[TargetSystemId] = C.[TargetSystemId]
        -- Re-checks Enabled and the org match, which the caller's token already checked.
        INNER JOIN
            [dbo].[PamAccessConnector] AC ON AC.[Id] = @AccessConnectorId
                AND AC.[OrganizationId] = C.[OrganizationId]
                AND AC.[Status] = 0 -- Enabled
        WHERE
            J.[Id] = @JobId
            AND J.[Action] = 0 -- None
            AND J.[ExpirationDate] > @Now
            AND J.[NextClaimableDate] <= @Now
            AND C.[Enabled] = 1
            AND T.[Status] = 0 -- Active

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION

            -- Unknown job or one outside this access connector's assignment share NotEligible; no existence oracle.
            SET @Outcome = CASE
                WHEN NOT EXISTS (
                    SELECT
                        1
                    FROM
                        [dbo].[PamRotationJob] J2
                    INNER JOIN
                        [dbo].[PamRotationConfig] C2 ON C2.[Id] = J2.[RotationConfigId]
                    INNER JOIN
                        [dbo].[PamAccessConnectorTargetAssignment] A2 ON A2.[AccessConnectorId] = @AccessConnectorId
                            AND A2.[TargetSystemId] = C2.[TargetSystemId]
                    INNER JOIN
                        [dbo].[PamAccessConnector] AC2 ON AC2.[Id] = @AccessConnectorId
                            AND AC2.[OrganizationId] = C2.[OrganizationId]
                            AND AC2.[Status] = 0 -- Enabled
                    WHERE
                        J2.[Id] = @JobId
                ) THEN -1 -- NotEligible: unknown, outside the assignment or org, or the access connector is disabled
                ELSE 0 -- NotClaimable: eligible, but not Pending, in backoff, or its config or target is disabled
            END

            SELECT
                @Outcome AS [Outcome],
                CAST(NULL AS UNIQUEIDENTIFIER) AS [AttemptId],
                CAST(NULL AS UNIQUEIDENTIFIER) AS [JobId],
                CAST(NULL AS TINYINT) AS [Source],
                CAST(NULL AS UNIQUEIDENTIFIER) AS [TargetSystemId],
                CAST(NULL AS NVARCHAR(200)) AS [TargetSystemName],
                CAST(NULL AS NVARCHAR(MAX)) AS [TargetSystemSealedData],
                CAST(NULL AS NVARCHAR(MAX)) AS [TargetSystemWrappedKey],
                CAST(NULL AS UNIQUEIDENTIFIER) AS [CipherId],
                CAST(NULL AS NVARCHAR(MAX)) AS [RotationConfigSealedData],
                CAST(NULL AS NVARCHAR(MAX)) AS [RotationConfigWrappedKey],
                CAST(NULL AS DATETIME2(7)) AS [ExecuteBy]
            RETURN
        END

        -- AtMostOneInFlightAttemptPerJob: the attempt is created in the same transaction as the claim.
        -- Its CreationDate equals the job's ClaimedDate, which is how the claim's own attempt is recognised later.
        INSERT INTO [dbo].[PamRotationAttempt]
        (
            [Id],
            [JobId],
            [ClaimedByAccessConnectorId],
            [CipherUpdated],
            [Action],
            [FailureReason],
            [SyncState],
            [SessionTermination],
            [CreationDate],
            [ResolvedDate]
        )
        VALUES
        (
            @AttemptId,
            @JobId,
            @AccessConnectorId,
            0,
            0, -- None
            NULL,
            NULL,
            NULL,
            @Now,
            NULL
        )

        COMMIT TRANSACTION

        -- ExecuteBy is this claim's lease end (ClaimedDate + ReleaseDelay).
        SELECT
            1 AS [Outcome], -- Claimed
            @AttemptId AS [AttemptId],
            J.[Id] AS [JobId],
            J.[Source],
            T.[Id] AS [TargetSystemId],
            T.[Name] AS [TargetSystemName],
            T.[SealedData] AS [TargetSystemSealedData],
            T.[WrappedKey] AS [TargetSystemWrappedKey],
            C.[CipherId],
            C.[SealedData] AS [RotationConfigSealedData],
            C.[WrappedKey] AS [RotationConfigWrappedKey],
            DATEADD(SECOND, @ReleaseDelaySeconds, @Now) AS [ExecuteBy]
        FROM
            [dbo].[PamRotationJob] J
        INNER JOIN
            [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
        INNER JOIN
            [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
        WHERE
            J.[Id] = @JobId
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @SealedData NVARCHAR(MAX) = NULL,
    @WrappedKey NVARCHAR(MAX) = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamTargetSystem]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Method],
        [SealedData],
        [WrappedKey],
        [Status],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Method,
        @SealedData,
        @WrappedKey,
        @Status,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @SealedData NVARCHAR(MAX),
    @WrappedKey NVARCHAR(MAX),
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[PamTargetSystem]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Method] = @Method,
        [SealedData] = @SealedData,
        [WrappedKey] = @WrappedKey,
        [Status] = @Status,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
