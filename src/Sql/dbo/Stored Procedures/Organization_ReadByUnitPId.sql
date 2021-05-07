CREATE PROCEDURE [dbo].[Organization_ReadByUnitPId]
    @UnitPId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        O.*
    FROM
        [dbo].[OrganizationView] O
    INNER JOIN
        [dbo].[UnitPOrganization] UO ON O.[Id] = UO.[OrganizationId]
    WHERE
        UO.[UnitPId] = @UnitPId
END
