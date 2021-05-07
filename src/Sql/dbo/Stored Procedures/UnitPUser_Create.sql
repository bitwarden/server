CREATE PROCEDURE [dbo].[UnitPUser_Create]
    @Id UNIQUEIDENTIFIER,
    @UnitPId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[UnitPUser]
    (
        [Id],
        [UnitPId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UnitPId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @CreationDate,
        @RevisionDate
    )
END
