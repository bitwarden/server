CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_ReadMetricsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(CASE WHEN st.[Status] = 1 THEN 1 END) AS CompletedTasks,
        COUNT(*) AS TotalTasks
    FROM
        [dbo].[SecurityTaskView] st
    WHERE
        st.[OrganizationId] = @OrganizationId
END
GO
