CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_SyncMany]
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
