CREATE PROCEDURE [dbo].[History_ReadById]
    @Id BIGINT
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[HistoryView]
    WHERE
        [Id] = @Id
END
