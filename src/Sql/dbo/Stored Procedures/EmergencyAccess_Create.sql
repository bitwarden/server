CREATE PROCEDURE [dbo].[EmergencyAccess_Create]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @KeyEncrypted VARCHAR(MAX),
    @Type TINYINT,
    @Status TINYINT,
    @WaitTimeDays SMALLINT,
    @RecoveryInitiatedAt DATETIME2(7),
    @LastNotificationAt DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[EmergencyAccess]
    (
        [Id],
        [GrantorId],
        [GranteeId],
        [Email],
        [KeyEncrypted],
        [Type],
        [Status],
        [WaitTimeDays],
        [RecoveryInitiatedAt],
        [LastNotificationAt],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @GrantorId,
        @GranteeId,
        @Email,
        @KeyEncrypted,
        @Type,
        @Status,
        @WaitTimeDays,
        @RecoveryInitiatedAt,
        @LastNotificationAt,
        @CreationDate,
        @RevisionDate
    )
END