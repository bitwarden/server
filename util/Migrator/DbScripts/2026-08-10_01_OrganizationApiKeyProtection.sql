-- Data-protect OrganizationApiKey.ApiKey at rest (PM-40439 / VULN-679): widen the column so it
-- can hold an IDataProtector-wrapped value ("P|" prefix + base64 payload), widen the write procs,
-- and add the raw keyset read + compare-and-swap update used by the backfill migration job.

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'OrganizationApiKey'
        AND COLUMN_NAME = 'ApiKey'
        AND CHARACTER_MAXIMUM_LENGTH = 30)
BEGIN
    ALTER TABLE [dbo].[OrganizationApiKey]
        ALTER COLUMN [ApiKey] VARCHAR(300) NOT NULL;
END
GO

EXECUTE sp_refreshview N'[dbo].[OrganizationApiKeyView]';
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_ReadManyByOrganizationIdType]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationApiKey_ReadManyByOrganizationIdType]';
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApiKey_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(300),
    @Type TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationApiKey]
    (
        [Id],
        [OrganizationId],
        [ApiKey],
        [Type],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @ApiKey,
        @Type,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApiKey_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @ApiKey VARCHAR(300),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationApiKey]
    SET
        [ApiKey] = @ApiKey,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApiKey_ReadCount]
AS
BEGIN
    SET NOCOUNT ON

    -- One-time anchor for the protection migration's pending-rows metric and partition split.
    SELECT COUNT_BIG(*)
    FROM [dbo].[OrganizationApiKey]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApiKey_ReadManyUnprotectedAfterId]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApiKey_UpdateManyApiKeys]
    @Updates NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Set-based compare-and-swap: the OriginalApiKey predicate means a concurrently rotated key
    -- never gets clobbered — the rotation's write wins and this row simply doesn't match.
    -- RevisionDate is intentionally NOT bumped: it is surfaced to admins as the key's rotation
    -- date, and protecting the value at rest is not a rotation.
    UPDATE OAK
    SET
        [ApiKey] = U.[ProtectedApiKey]
    FROM [dbo].[OrganizationApiKey] OAK
    INNER JOIN OPENJSON(@Updates) WITH (
        [Id] UNIQUEIDENTIFIER '$.id',
        [OriginalApiKey] VARCHAR(300) '$.originalApiKey',
        [ProtectedApiKey] VARCHAR(300) '$.protectedApiKey'
    ) U ON OAK.[Id] = U.[Id]
    WHERE OAK.[ApiKey] = U.[OriginalApiKey]

    SELECT @@ROWCOUNT
END
GO
