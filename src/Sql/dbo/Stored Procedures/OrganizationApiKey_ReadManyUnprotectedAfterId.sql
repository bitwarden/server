CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadManyUnprotectedAfterId]
    @Cursor UNIQUEIDENTIFIER,
    @ScanWindow INT,
    @BatchSize INT,
    @ProtectedPrefix VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON

    -- Windowed migration read: examine exactly the next @ScanWindow rows (bounded, predictable
    -- statement cost regardless of candidate density), but ship only the unprotected candidates
    -- plus one metadata row carrying the scan high-water mark. The cursor must track how far we
    -- LOOKED, not how far we converted — otherwise a candidate-free window would never advance.
    -- Returns RAW stored values on purpose: the migration must see the column verbatim.
    ;WITH [Window] AS (
        SELECT TOP (@ScanWindow)
            [Id],
            [ApiKey]
        FROM [dbo].[OrganizationApiKey]
        WHERE [Id] > @Cursor
        ORDER BY [Id] ASC
    )
    SELECT
        [Id],
        [ApiKey],
        [IsWindowMetadata],
        [ScannedCount],
        [CandidateCount]
    FROM
    (
        SELECT TOP (@BatchSize)
            [Id],
            [ApiKey],
            CAST(0 AS BIT) AS [IsWindowMetadata],
            0 AS [ScannedCount],
            0 AS [CandidateCount]
        FROM [Window]
        WHERE [ApiKey] NOT LIKE @ProtectedPrefix + '%'
        ORDER BY [Id] ASC

        UNION ALL

        SELECT
            MAX([Id]),
            NULL,
            CAST(1 AS BIT),
            COUNT(*),
            SUM(CASE WHEN [ApiKey] NOT LIKE @ProtectedPrefix + '%' THEN 1 ELSE 0 END)
        FROM [Window]
    ) R
    ORDER BY [IsWindowMetadata] ASC, [Id] ASC
END
