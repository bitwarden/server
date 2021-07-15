CREATE PROCEDURE [dbo].[ProviderUser_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @Permissions NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderUser]
    (
        [Id],
        [ProviderId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [Permissions],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @Permissions,
        @CreationDate,
        @RevisionDate
    )
END
