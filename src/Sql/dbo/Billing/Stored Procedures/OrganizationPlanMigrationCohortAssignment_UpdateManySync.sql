CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_UpdateManySync]
    @JsonData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Source TABLE (
        [Id]             UNIQUEIDENTIFIER,
        [OrganizationId] UNIQUEIDENTIFIER,
        [CohortId]       UNIQUEIDENTIFIER NULL
    )

    INSERT INTO @Source
    (
        [Id],
        [OrganizationId],
        [CohortId]
    )
    SELECT
        [Id],
        [OrganizationId],
        [CohortId]
    FROM
        OPENJSON(@JsonData)
        WITH (
            [Id]             UNIQUEIDENTIFIER '$.Id',
            [OrganizationId] UNIQUEIDENTIFIER '$.OrganizationId',
            [CohortId]       UNIQUEIDENTIFIER '$.CohortId'
        )

    -- Locked orgs: existing assignment is scheduled to migrate or has already migrated (migration
    -- cohort), or has had a churn discount applied (non-migration cohort). These orgs are
    -- protected from change.
    DECLARE @Locked TABLE ([OrganizationId] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @Locked ([OrganizationId])
    SELECT
        A.[OrganizationId]
    FROM
        @Source S
    INNER JOIN
        [dbo].[OrganizationPlanMigrationCohortAssignmentView] A ON A.[OrganizationId] = S.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationPlanMigrationCohortView] C ON C.[Id] = A.[CohortId]
    WHERE
        (C.[MigrationPathId] IS NOT NULL AND (A.[ScheduledDate] IS NOT NULL OR A.[MigratedDate] IS NOT NULL))
        OR
        (C.[MigrationPathId] IS NULL AND A.[ChurnDiscountAppliedDate] IS NOT NULL)

    -- Count locked rows the CSV would have CHANGED (reassign or unassign). No-op rows
    -- (same cohort) are not counted -- nothing would have happened regardless of the lock.
    DECLARE @Skipped INT

    SELECT
        @Skipped = COUNT(1)
    FROM
        @Source S
    INNER JOIN
        [dbo].[OrganizationPlanMigrationCohortAssignmentView] A ON A.[OrganizationId] = S.[OrganizationId]
    INNER JOIN
        @Locked L ON L.[OrganizationId] = S.[OrganizationId]
    WHERE
        S.[CohortId] IS NULL
        OR S.[CohortId] <> A.[CohortId]

    DECLARE @Outcomes TABLE ([Action] NVARCHAR(10))

    MERGE [dbo].[OrganizationPlanMigrationCohortAssignment] AS [Target]
    USING (
        SELECT
            [Id],
            [OrganizationId],
            [CohortId]
        FROM
            @Source S
        WHERE
            NOT EXISTS (SELECT 1 FROM @Locked L WHERE L.[OrganizationId] = S.[OrganizationId])
    ) AS [Source]
        ON [Target].[OrganizationId] = [Source].[OrganizationId]
    WHEN MATCHED AND [Source].[CohortId] IS NULL THEN
        DELETE
    WHEN MATCHED AND [Target].[CohortId] <> [Source].[CohortId] THEN
        UPDATE SET
            [CohortId]     = [Source].[CohortId],
            [RevisionDate] = GETUTCDATE()
    WHEN NOT MATCHED BY TARGET AND [Source].[CohortId] IS NOT NULL THEN
        INSERT
        (
            [Id],
            [OrganizationId],
            [CohortId],
            [CreationDate],
            [RevisionDate]
        )
        VALUES
        (
            [Source].[Id],
            [Source].[OrganizationId],
            [Source].[CohortId],
            GETUTCDATE(),
            GETUTCDATE()
        )
    OUTPUT $action INTO @Outcomes;

    SELECT
        ISNULL(SUM(CASE WHEN [Action] = 'INSERT' THEN 1 ELSE 0 END), 0) AS [Inserted],
        ISNULL(SUM(CASE WHEN [Action] = 'UPDATE' THEN 1 ELSE 0 END), 0) AS [Updated],
        ISNULL(SUM(CASE WHEN [Action] = 'DELETE' THEN 1 ELSE 0 END), 0) AS [Unassigned],
        @Skipped AS [Skipped]
    FROM
        @Outcomes
END
