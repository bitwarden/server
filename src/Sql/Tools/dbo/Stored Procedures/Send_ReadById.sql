CREATE PROCEDURE [dbo].[Send_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [Id] = @Id
END