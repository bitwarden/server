CREATE PROCEDURE [dbo].[EmergencyAccess_Update]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @KeyEncrypted VARCHAR(MAX),
    @Type TINYINT,
    @Status TINYINT,
    @WaitTimeDays SMALLINT,
    @RecoveryInitiatedDate DATETIME2(7),
    @LastNotificationDate DATETIME2(7),
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
        [RecoveryInitiatedDate] = @RecoveryInitiatedDate,
        [LastNotificationDate] = @LastNotificationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @GranteeId
END