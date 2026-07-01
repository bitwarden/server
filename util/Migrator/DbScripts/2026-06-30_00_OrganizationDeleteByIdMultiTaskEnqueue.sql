-- Generalize the atomic OrganizationDeleteTask enqueue in Organization_DeleteById so
-- any number of task types can be enqueued in the same transaction as the delete.
-- Adds a table-valued parameter; the existing scalar params are retained (defaulted)
-- so callers on the previous server version keep working during a rolling deployment.

-- User-defined table type carrying the tasks to enqueue.
IF TYPE_ID('[dbo].[OrganizationDeleteTaskArray]') IS NULL
BEGIN
    CREATE TYPE [dbo].[OrganizationDeleteTaskArray] AS TABLE (
        [Id]           UNIQUEIDENTIFIER NOT NULL,
        [TaskType]     TINYINT          NOT NULL,
        [CreationDate] DATETIME2(7)     NOT NULL);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER,
    @OrganizationDeleteTaskId UNIQUEIDENTIFIER = NULL,
    @OrganizationDeleteTaskType TINYINT = NULL,
    @OrganizationDeleteTaskCreationDate DATETIME2(7) = NULL,
    @OrganizationDeleteTasks [dbo].[OrganizationDeleteTaskArray] READONLY
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE AP
    FROM
        [dbo].[AccessPolicy] AP
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [AP].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [GU].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationDomain_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationIntegration_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Project]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[Secret]
    WHERE
        [OrganizationId] = @Id

    DELETE AK
    FROM
        [dbo].[ApiKey] AK
    INNER JOIN
        [dbo].[ServiceAccount] SA ON [AK].[ServiceAccountId] = [SA].[Id]
    WHERE
        [SA].[OrganizationId] = @Id

    DELETE AP
    FROM
        [dbo].[AccessPolicy] AP
    INNER JOIN
        [dbo].[ServiceAccount] SA ON [AP].[GrantedServiceAccountId] = [SA].[Id]
    WHERE
        [SA].[OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[ServiceAccount]
    WHERE
        [OrganizationId] = @Id

    -- Delete Notification Status
    DELETE
        NS
    FROM
        [dbo].[NotificationStatus] NS
    INNER JOIN
        [dbo].[Notification] N ON N.[Id] = NS.[NotificationId]
    WHERE
        N.[OrganizationId] = @Id

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Application
    DELETE
    FROM
        [dbo].[OrganizationApplication]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Report
    DELETE
    FROM
        [dbo].[OrganizationReport]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Owned Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [OrganizationId] = @Id

    -- Atomically enqueue one or more OrganizationDeleteTasks (e.g. for purging Table
    -- Storage event logs) so downstream cleanup is durably recorded with the deletion.
    -- Preferred path: a set of tasks passed via table-valued parameter, letting any
    -- number of teams enqueue their own cleanup type in the same transaction.
    IF EXISTS (SELECT 1 FROM @OrganizationDeleteTasks)
    BEGIN
        INSERT INTO [dbo].[OrganizationDeleteTask]
        (
            [Id],
            [OrganizationId],
            [TaskType],
            [CreationDate],
            [RevisionDate]
        )
        SELECT
            [Id],
            @Id,
            [TaskType],
            [CreationDate],
            [CreationDate]
        FROM
            @OrganizationDeleteTasks
    END
    -- Legacy single-task path. Retained so callers still running the previous server
    -- version keep working during a rolling deployment; safe to remove once fully rolled.
    ELSE IF @OrganizationDeleteTaskId IS NOT NULL
    BEGIN
        DECLARE @OrganizationDeleteTaskDate DATETIME2(7) = COALESCE(@OrganizationDeleteTaskCreationDate, SYSUTCDATETIME())

        INSERT INTO [dbo].[OrganizationDeleteTask]
        (
            [Id],
            [OrganizationId],
            [TaskType],
            [CreationDate],
            [RevisionDate]
        )
        VALUES
        (
            @OrganizationDeleteTaskId,
            @Id,
            @OrganizationDeleteTaskType,
            @OrganizationDeleteTaskDate,
            @OrganizationDeleteTaskDate
        )
    END

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO
