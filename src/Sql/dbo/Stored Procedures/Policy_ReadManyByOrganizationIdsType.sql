CREATE PROCEDURE [dbo].[Policy_ReadManyByOrganizationIdsType]
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        P.*
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        @OrganizationIds I ON P.[OrganizationId] = I.[Id]
    WHERE
        P.[Type] = @Type
END
