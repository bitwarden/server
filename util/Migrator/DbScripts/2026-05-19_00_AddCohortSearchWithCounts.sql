CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_SearchWithCounts]
    @Name NVARCHAR(255) = NULL,
    @Skip INT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[OrganizationPlanMigrationCohortView]
    WHERE @Name IS NULL OR [Name] LIKE '%' + @Name + '%'
    ORDER BY [CreationDate] DESC, [Id] ASC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
END
GO
