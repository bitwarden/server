CREATE PROCEDURE [dbo].[Installation_Update]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Installation]
    SET
        [Email] = @Email,
        [Key] = @Key,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
END