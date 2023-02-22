CREATE PROCEDURE [dbo].[Organization_UnassignedToProviderSearch]
    @Name NVARCHAR(50),
    @OwnerEmail NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @NameLikeSearch NVARCHAR(55) = '%' + @Name + '%'
    DECLARE @OwnerEmailLikeSearch NVARCHAR(258) = '%' + @OwnerEmail + '%'

SELECT
    Q.*
FROM (
         SELECT DISTINCT
             O.*,
             (
                 SELECT STRING_AGG(U.Email, ', ') WITHIN GROUP (ORDER BY U.Email ASC)
                 FROM
                     [dbo].[OrganizationUser] OU
                 INNER JOIN
                     [dbo].[User] U ON U.[Id] = OU.[UserId]
                 WHERE
                     OU.[OrganizationId] = O.Id
                    AND OU.[Type] = 0 --Get 'Owner' type users only
                 GROUP BY OU.[OrganizationId]
             ) [OwnerEmails]
FROM [dbo].[OrganizationView] O
    ) Q
WHERE
    Q.[PlanType] >= 8 AND Q.[PlanType] <= 11 -- Get 'Team' and 'Enterprise' Organizations
    AND NOT EXISTS (SELECT * FROM [dbo].[ProviderOrganizationView] po WHERE po.[OrganizationId] = Q.[Id])
    AND (@Name IS NULL OR Q.[Name] LIKE @NameLikeSearch)
    AND (@OwnerEmail IS NULL OR Q.[OwnerEmails] LIKE @OwnerEmailLikeSearch)
ORDER BY Q.[CreationDate] DESC
OFFSET @Skip ROWS
FETCH NEXT @Take ROWS ONLY
END
