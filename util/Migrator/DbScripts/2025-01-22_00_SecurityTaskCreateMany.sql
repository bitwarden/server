-- SecurityTask_CreateMany
CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_CreateMany]
    @SecurityTasksJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #TempSecurityTasks
    (
        [Id]             UNIQUEIDENTIFIER,
        [OrganizationId] UNIQUEIDENTIFIER,
        [CipherId]       UNIQUEIDENTIFIER,
        [Type]           TINYINT,
        [Status]         TINYINT,
        [CreationDate]   DATETIME2(7),
        [RevisionDate]   DATETIME2(7)
    )

    INSERT INTO #TempSecurityTasks
    ([Id],
     [OrganizationId],
     [CipherId],
     [Type],
     [Status],
     [CreationDate],
     [RevisionDate])
    SELECT CAST(JSON_VALUE([value], '$.Id') AS UNIQUEIDENTIFIER),
           CAST(JSON_VALUE([value], '$.OrganizationId') AS UNIQUEIDENTIFIER),
           CAST(JSON_VALUE([value], '$.CipherId') AS UNIQUEIDENTIFIER),
           CAST(JSON_VALUE([value], '$.Type') AS TINYINT),
           CAST(JSON_VALUE([value], '$.Status') AS TINYINT),
           CAST(JSON_VALUE([value], '$.CreationDate') AS DATETIME2(7)),
           CAST(JSON_VALUE([value], '$.RevisionDate') AS DATETIME2(7))
    FROM OPENJSON(@SecurityTasksJson) ST

    INSERT INTO [dbo].[SecurityTask]
    ([Id],
     [OrganizationId],
     [CipherId],
     [Type],
     [Status],
     [CreationDate],
     [RevisionDate])
    SELECT [Id],
           [OrganizationId],
           [CipherId],
           [Type],
           [Status],
           [CreationDate],
           [RevisionDate]
    FROM #TempSecurityTasks

    DROP TABLE #TempSecurityTasks
END
GO
