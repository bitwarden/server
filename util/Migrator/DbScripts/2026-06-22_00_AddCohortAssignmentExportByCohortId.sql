-- PM-36965 CSV export of a migration cohort.
-- Adds a keyset-paging index over (CohortId, CreationDate, Id) and the bounded export read proc.

IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_OrganizationPlanMigrationCohortAssignment_CohortId_CreationDate_Id'
        AND object_id = OBJECT_ID('[dbo].[OrganizationPlanMigrationCohortAssignment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_CreationDate_Id]
        ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [CreationDate] ASC, [Id] ASC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadManyExportByCohortId]
    @CohortId UNIQUEIDENTIFIER,
    @AfterCreationDate DATETIME2(7) = NULL,
    @AfterId UNIQUEIDENTIFIER = NULL,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON

    -- One bounded keyset page of cohort assignments, joined to the organization to surface its
    -- display name. Ordered by (CreationDate, Id) so the IX_..._CohortId_CreationDate_Id index
    -- serves both the seek and the sort with no residual Sort operator -- important because a
    -- bulk-loaded cohort shares a single CreationDate across the whole batch, which would otherwise
    -- collapse the entire ordering onto the tiebreaker. The cursor only needs to be internally
    -- consistent (the WHERE seek matches this ORDER BY); CSV row order need not match other database
    -- providers, since the export is consumed as a download (every row, once).
    SELECT TOP (@Take)
        A.[Id],
        A.[OrganizationId],
        O.[Name] AS [OrganizationName],
        A.[CreationDate] AS [AssignedAt],
        A.[ScheduledDate],
        A.[MigratedDate]
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignment] A
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = A.[OrganizationId]
    WHERE
        A.[CohortId] = @CohortId
        AND (
            @AfterCreationDate IS NULL
            OR A.[CreationDate] > @AfterCreationDate
            OR (A.[CreationDate] = @AfterCreationDate AND A.[Id] > @AfterId)
        )
    ORDER BY
        A.[CreationDate] ASC,
        A.[Id] ASC
END
GO
