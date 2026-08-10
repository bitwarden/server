CREATE PROCEDURE [dbo].[DataMigrationState_ReadCountByName]
    @Name VARCHAR(100),
    @IncompleteOnly BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT COUNT(*)
    FROM [dbo].[DataMigrationState]
    WHERE
        [Name] = @Name
        AND (@IncompleteOnly = 0 OR [CompletedDate] IS NULL)
END
