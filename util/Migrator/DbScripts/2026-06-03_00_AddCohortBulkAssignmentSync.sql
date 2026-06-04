-- PM-36963 bulk cohort assignment: projection view + read/sync procs.

CREATE OR ALTER VIEW [dbo].[OrganizationPlanTypeView]
AS
SELECT
    [Id] AS [OrganizationId],
    [PlanType]
FROM
    [dbo].[Organization]
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadPlanTypesByIds]
    @OrganizationIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanTypeView]
    WHERE
        [OrganizationId] IN (SELECT [Id] FROM @OrganizationIds)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadManyByNames]
    @JsonData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*
    FROM
        [dbo].[OrganizationPlanMigrationCohortView] C
    INNER JOIN
        OPENJSON(@JsonData) WITH ([Name] NVARCHAR(255) '$.Name') N ON C.[Name] = N.[Name]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_SyncMany]
    @JsonData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Outcomes TABLE ([Action] NVARCHAR(10))

    MERGE [dbo].[OrganizationPlanMigrationCohortAssignment] AS [Target]
    USING (
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
        ISNULL(SUM(CASE WHEN [Action] = 'DELETE' THEN 1 ELSE 0 END), 0) AS [Unassigned]
    FROM
        @Outcomes
END
GO
