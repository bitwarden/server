CREATE PROCEDURE [dbo].[ConnectedApp_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[ConnectedApp]
    (
        [Id],
        [UserId],
        [PartnerClientId],
        [Name],
        [Key],
        [CreationDate],
        [RevisionDate],
        [RevokedDate],
        [Type]
    )
    VALUES
    (
        @Id,
        @UserId,
        @PartnerClientId,
        @Name,
        @Key,
        @CreationDate,
        @RevisionDate,
        @RevokedDate,
        @Type
    )

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
