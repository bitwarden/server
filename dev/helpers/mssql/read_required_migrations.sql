-- Create a stored procedure
CREATE OR ALTER PROCEDURE ReadRequiredMigrations
AS
BEGIN
    SET NOCOUNT ON;

    -- Create a temporary table to store the input Filenames
    CREATE TABLE #InputMigrations (Filename NVARCHAR(MAX))

    BULK INSERT #InputMigrations FROM "/mnt/helpers/all_migrations.txt" WITH (FIELDTERMINATOR = ';', ROWTERMINATOR = ';')

    -- Select migrations that do not appear in the inputMigrations table using a LEFT JOIN
    SELECT IM.[Filename]
    FROM [#InputMigrations] IM
    LEFT JOIN [dbo].[migrations] M ON IM.[Filename] LIKE '%' + M.[Filename] + '%'
    WHERE M.[Filename] IS NULL
    ORDER BY [Filename] ASC

    -- Drop the temporary table
    DROP TABLE #InputMigrations
END
