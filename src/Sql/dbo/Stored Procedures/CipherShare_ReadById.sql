CREATE PROCEDURE [dbo].[CipherShare_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherShareView]
    WHERE
        [Id] = @Id
END