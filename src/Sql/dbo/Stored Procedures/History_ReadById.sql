CREATE PROCEDURE [dbo].[History_ReadById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[HistoryView]
    WHERE
        [Id] = @Id
END