CREATE OR ALTER PROCEDURE ReadRequiredMigrations
    @migrationsFile NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #InputMigrations (Filename NVARCHAR(MAX))

    DECLARE @bulkInsertSql NVARCHAR(4000) = 'BULK INSERT #InputMigrations FROM ''' + @migrationsFile + ''' WITH (FIELDTERMINATOR = '';'', ROWTERMINATOR = '';'')';
    EXEC(@bulkInsertSql);

    -- Select migrations that do not appear in the [dbo].[Migrations] table
    SELECT IM.[Filename]
    FROM [#InputMigrations] IM
    LEFT JOIN [dbo].[migrations] M ON IM.[Filename] = M.[Filename]
    WHERE M.[Filename] IS NULL
    ORDER BY [Filename] ASC

    DROP TABLE #InputMigrations
END
