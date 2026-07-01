CREATE OR ALTER PROCEDURE [dbo].[EmergencyAccess_UpdateStatusKeyEncryptedById]
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

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateStatusKeyById]
    @Id UNIQUEIDENTIFIER,
    @Status SMALLINT,
    @Key VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = @Status,
        [Key] = @Key,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
