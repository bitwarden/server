CREATE PROCEDURE [dbo].[Installation_Create]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Installation]
    (
        [Id],
        [Email],
        [Key],
        [Enabled],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @Email,
        @Key,
        @Enabled,
        @CreationDate
    )
END