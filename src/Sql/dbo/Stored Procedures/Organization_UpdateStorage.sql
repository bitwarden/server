CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    CREATE TABLE #OrgStorageUpdateTemp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #OrgStorageUpdateTemp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @Id

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            #OrgStorageUpdateTemp
    )
    SELECT
        @Storage = SUM([Size])
    FROM
        [CTE]

    DROP TABLE #OrgStorageUpdateTemp

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END