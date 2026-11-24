CREATE PROCEDURE [dbo].[ConnectedApp_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @PartnerClientId NVARCHAR(200),
    @Name NVARCHAR(200),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RevokedDate DATETIME2(7) = NULL,
    @Type TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ConnectedApp]
    SET
        [UserId] = @UserId,
        [PartnerClientId] = @PartnerClientId,
        [Name] = @Name,
        [Key] = @Key,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [RevokedDate] = @RevokedDate,
        [Type] = @Type
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
