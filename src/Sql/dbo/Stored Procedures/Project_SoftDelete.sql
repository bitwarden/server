CREATE PROCEDURE [dbo].[Project_SoftDelete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id]
    FROM
        [dbo].[Project]  
    WHERE
        OrganizationId = @OrganizationId
        AND [DeletedDate] IS NULL
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete Project
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Project]
    SET
        [DeletedDate] = @UtcNow,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    DROP TABLE #Temp
END

