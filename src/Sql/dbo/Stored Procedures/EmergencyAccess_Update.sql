CREATE PROCEDURE [dbo].[EmergencyAccess_Update]
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

    UPDATE
        [dbo].[EmergencyAccess]
    SET
        [GrantorId] = @GrantorId,
        [GranteeId] = @GranteeId,
        [Email] = @Email,
        [KeyEncrypted] = @KeyEncrypted,
        [Type] = @Type,
        [Status] = @Status,
        [WaitTimeDays] = @WaitTimeDays,
        [RecoveryInitiatedAt] = @RecoveryInitiatedAt,
        [LastNotificationAt] = @LastNotificationAt,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @GranteeId
END