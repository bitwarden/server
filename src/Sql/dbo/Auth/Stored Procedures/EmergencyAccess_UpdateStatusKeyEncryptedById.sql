CREATE PROCEDURE [dbo].[EmergencyAccess_UpdateStatusKeyEncryptedById]
    @Id UNIQUEIDENTIFIER,
    @Status TINYINT,
    @KeyEncrypted VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[EmergencyAccess]
    SET
        [Status] = @Status,
        [KeyEncrypted] = @KeyEncrypted,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
