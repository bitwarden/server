CREATE PROCEDURE [dbo].[Organization_ReadPlanTypesByIds]
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
