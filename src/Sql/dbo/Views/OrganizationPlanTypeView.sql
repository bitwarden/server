CREATE VIEW [dbo].[OrganizationPlanTypeView]
AS
SELECT
    [Id] AS [OrganizationId],
    [PlanType]
FROM
    [dbo].[Organization]
