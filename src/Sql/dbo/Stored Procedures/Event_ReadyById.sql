CREATE PROCEDURE [dbo].[Event_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[Event]
    WHERE
        [Id] = @Id
END
