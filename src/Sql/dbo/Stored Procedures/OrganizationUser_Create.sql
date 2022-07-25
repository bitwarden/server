CREATE PROCEDURE [dbo].[OrganizationUser_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Permissions NVARCHAR(MAX),
    @ResetPasswordKey VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationUser]
    (
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions],
        [ResetPasswordKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate,
        @Permissions,
        @ResetPasswordKey
    )
END
