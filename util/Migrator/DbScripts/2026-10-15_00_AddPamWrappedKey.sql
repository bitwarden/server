-- Adds a nullable WrappedKey column to PamTargetSystem and PamRotationConfig.
-- Existing rows keep NULL and must be re-saved.

IF COL_LENGTH('[dbo].[PamTargetSystem]', 'WrappedKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[PamTargetSystem] ADD [WrappedKey] NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('[dbo].[PamRotationConfig]', 'WrappedKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[PamRotationConfig] ADD [WrappedKey] NVARCHAR(MAX) NULL;
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
    @WrappedKey NVARCHAR(MAX),
    @ScheduleCron NVARCHAR(100) = NULL,
    @RotateOnAccessEnd BIT,
    @NextRotationAt DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationAt DATETIME2(7) = NULL,
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
        [NextRotationAt],
        [Enabled],
        [LastRotationAt],
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
        @NextRotationAt,
        @Enabled,
        @LastRotationAt,
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
    @NextRotationAt DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationAt DATETIME2(7) = NULL,
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
        [NextRotationAt] = @NextRotationAt,
        [Enabled] = @Enabled,
        [LastRotationAt] = @LastRotationAt,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_Claim]
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

    BEGIN TRANSACTION

    UPDATE J
    SET J.[Action] = 1, -- Claimed
        J.[ClaimedByAccessConnectorId] = @AccessConnectorId,
        J.[ClaimedAt] = @Now
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamAccessConnectorTargetAssignment] A ON A.[AccessConnectorId] = @AccessConnectorId AND A.[TargetSystemId] = C.[TargetSystemId]
    -- Re-checks Enabled and the org match, which the caller's token already checked.
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = @AccessConnectorId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Id] = @JobId
        AND J.[Action] = 0 -- None
        AND J.[ExpiresAt] > @Now
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active

    IF @@ROWCOUNT = 0
    BEGIN
        -- Unknown job or one outside this access connector's assignment share NotEligible; no existence oracle.
        DECLARE @Outcome INT = CASE
            WHEN NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJob] J2
                INNER JOIN [dbo].[PamRotationConfig] C2 ON C2.[Id] = J2.[RotationConfigId]
                INNER JOIN [dbo].[PamAccessConnectorTargetAssignment] A2 ON A2.[AccessConnectorId] = @AccessConnectorId AND A2.[TargetSystemId] = C2.[TargetSystemId]
                INNER JOIN [dbo].[PamAccessConnector] D2 ON D2.[Id] = @AccessConnectorId AND D2.[OrganizationId] = C2.[OrganizationId] AND D2.[Status] = 0 -- Enabled
                WHERE J2.[Id] = @JobId
            ) THEN -1 -- NotEligible: unknown, outside the assignment or org, or the access connector is disabled
            ELSE 0 -- NotClaimable: eligible, but not Pending, in backoff, or its config or target is disabled
        END

        ROLLBACK TRANSACTION

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
    -- Its CreationDate equals the job's ClaimedAt, which is how the claim's own attempt is recognised later.
    INSERT INTO [dbo].[PamRotationAttempt]
    (
        [Id], [JobId], [ClaimedByAccessConnectorId], [CipherUpdated], [Action], [FailureReason], [SyncState],
        [SessionTermination], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AttemptId, @JobId, @AccessConnectorId, 0, 0 /* None */, NULL, NULL,
        NULL, @Now, NULL
    )

    COMMIT TRANSACTION

    -- ExecuteBy is this claim's lease end (ClaimedAt + ReleaseDelay).
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
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE J.[Id] = @JobId
END
GO

-- Refreshes cached metadata on every other module that plain-SELECTs the altered tables.
EXECUTE sp_refreshsqlmodule N'[dbo].[PamTargetSystem_ReadById]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamTargetSystem_ReadByOrganizationId]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamRotationConfig_ReadById]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamRotationConfig_ReadByCipherId]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamRotationConfig_ReadDetailsById]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamRotationConfig_ReadManyByOrganizationId]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamRotationConfig_ReadManyDue]'
GO
EXECUTE sp_refreshsqlmodule N'[dbo].[PamRotationJob_ReadManyClaimableByAccessConnectorId]'
GO
