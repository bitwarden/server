-- Table
IF OBJECT_ID('[dbo].[TwoFactorRememberToken]') IS NULL
BEGIN
    CREATE TABLE [dbo].[TwoFactorRememberToken]
    (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [UserId]         UNIQUEIDENTIFIER NOT NULL,
        [DeviceId]       UNIQUEIDENTIFIER NOT NULL,
        [Stamp]          NVARCHAR(50)     NOT NULL,
        [CreationDate]   DATETIME2(7)     NOT NULL,
        [RevisionDate]   DATETIME2(7)     NOT NULL,
        [ExpirationDate] DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_TwoFactorRememberToken] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_TwoFactorRememberToken_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
        CONSTRAINT [FK_TwoFactorRememberToken_Device] FOREIGN KEY ([DeviceId]) REFERENCES [dbo].[Device] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_TwoFactorRememberToken_UserId_DeviceId]
        ON [dbo].[TwoFactorRememberToken]([UserId] ASC, [DeviceId] ASC);

    CREATE NONCLUSTERED INDEX [IX_TwoFactorRememberToken_DeviceId]
        ON [dbo].[TwoFactorRememberToken]([DeviceId] ASC);

    CREATE NONCLUSTERED INDEX [IX_TwoFactorRememberToken_ExpirationDate]
        ON [dbo].[TwoFactorRememberToken]([ExpirationDate] ASC);
END
GO

-- View
CREATE OR ALTER VIEW [dbo].[TwoFactorRememberTokenView]
AS
SELECT
    *
FROM
    [dbo].[TwoFactorRememberToken]
GO

-- Stored Procedures
CREATE OR ALTER PROCEDURE [dbo].[TwoFactorRememberToken_Save]
    @Id             UNIQUEIDENTIFIER,
    @UserId         UNIQUEIDENTIFIER,
    @DeviceId       UNIQUEIDENTIFIER,
    @Stamp          NVARCHAR(50),
    @CreationDate   DATETIME2(7),
    @RevisionDate   DATETIME2(7),
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- An explicit transaction is required so the range lock below is still held when the INSERT
    -- runs; XACT_ABORT leaves the pooled connection clean if the unique-index backstop trips.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- One row per (UserId, DeviceId). The UPDLOCK, HOLDLOCK range lock serializes a concurrent
    -- remember-login on the same device: it blocks here until this transaction commits, then
    -- finds the row and takes the UPDATE branch instead of inserting a duplicate. The lock is
    -- cheap because [IX_TwoFactorRememberToken_UserId_DeviceId] makes it a key-range lock
    -- rather than a scan. CreationDate is deliberately not updated.
    UPDATE [dbo].[TwoFactorRememberToken] WITH (UPDLOCK, HOLDLOCK)
    SET
        [Stamp]          = @Stamp,
        [RevisionDate]   = @RevisionDate,
        [ExpirationDate] = @ExpirationDate
    WHERE
        [UserId] = @UserId
        AND [DeviceId] = @DeviceId

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[TwoFactorRememberToken]
        (
            [Id],
            [UserId],
            [DeviceId],
            [Stamp],
            [CreationDate],
            [RevisionDate],
            [ExpirationDate]
        )
        VALUES
        (
            @Id,
            @UserId,
            @DeviceId,
            @Stamp,
            @CreationDate,
            @RevisionDate,
            @ExpirationDate
        )
    END

    COMMIT TRANSACTION
END
GO

CREATE OR ALTER PROCEDURE [dbo].[TwoFactorRememberToken_ReadByUserIdDeviceId]
    @UserId   UNIQUEIDENTIFIER,
    @DeviceId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TwoFactorRememberTokenView]
    WHERE
        [UserId] = @UserId
        AND [DeviceId] = @DeviceId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[TwoFactorRememberToken_UpdateManyStampsByUserId]
    @UserId       UNIQUEIDENTIFIER,
    @Stamp        NVARCHAR(50),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Revokes every remember token this user holds by replacing the value each of their tokens is
    -- compared against. The rows are kept, so CreationDate still records when each device was first
    -- remembered and RevisionDate now records when it was cut off.
    --
    -- One stamp is shared across the user's rows, and the caller supplies it. Rows are located by
    -- (UserId, DeviceId), so the stamp never selects a row and a token naming one device can never
    -- match another's. Generating it in application code also keeps this procedure and the Entity
    -- Framework implementations writing identical values.
    UPDATE
        [dbo].[TwoFactorRememberToken]
    SET
        [Stamp] = @Stamp,
        [RevisionDate] = @RevisionDate
    WHERE
        [UserId] = @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[TwoFactorRememberToken_DeleteExpired]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Deleted in batches so each statement holds few enough locks to avoid escalating to a table
    -- lock, which would block logins while the sweep runs. The loop ends when a batch deletes
    -- nothing.
    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[TwoFactorRememberToken]
        WHERE
            [ExpirationDate] < @Now

        SET @BatchSize = @@ROWCOUNT
    END
END
GO
