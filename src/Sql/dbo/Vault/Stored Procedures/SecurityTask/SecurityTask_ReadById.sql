CREATE PROCEDURE [dbo].[SecurityTask_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SecurityTaskView]
    WHERE
        [Id] = @Id
END
