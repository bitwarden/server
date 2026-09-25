CREATE PROCEDURE [dbo].[TwoFactorRememberToken_Save]
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
