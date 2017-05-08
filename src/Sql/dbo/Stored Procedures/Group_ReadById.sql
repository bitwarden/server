CREATE PROCEDURE [dbo].[Group_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GroupView]
    WHERE
        [Id] = @Id
END