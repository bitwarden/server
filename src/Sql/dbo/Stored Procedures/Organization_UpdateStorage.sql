CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentStorage BIGINT
    DECLARE @SendStorage BIGINT

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
        @AttachmentStorage = SUM([Size])
    FROM
        [CTE]

    DROP TABLE #OrgStorageUpdateTemp

    ;WITH [CTE] AS (
        SELECT
            [Id],
            CAST(JSON_VALUE([Data],'$.Size') AS BIGINT) [Size]
        FROM
            [Send]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id
    )
    SELECT
        @SendStorage = SUM([CTE].[Size])
    FROM
        [CTE]

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = (ISNULL(@AttachmentStorage, 0) + ISNULL(@SendStorage, 0)),
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END