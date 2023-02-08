CREATE PROCEDURE [dbo].[ProviderOrganizationUnassignedOrganizationDetails_Search]
    @Name NVARCHAR(50),
    @OwnerEmail NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @NameLikeSearch NVARCHAR(55) = '%' + @Name + '%'

SELECT
    O.[Id] AS OrganizationId,
    O.[Name] AS Name,
    U.[Email] AS OwnerEmail,
    O.[PlanType] AS PlanType
FROM
    [dbo].[OrganizationView] O
INNER JOIN
    [dbo].[OrganizationUser] OU ON O.[Id] = OU.[OrganizationId]
INNER JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
WHERE
    NOT EXISTS (SELECT * FROM [dbo].[ProviderOrganizationView] po WHERE po.[OrganizationId] = O.[Id])
    AND OU.[Type] = 0 --Get 'Owner' type users only
    AND (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
    AND (@OwnerEmail IS NULL OR U.[Email] = @OwnerEmail)
ORDER BY O.[CreationDate] DESC
OFFSET @Skip ROWS
FETCH NEXT @Take ROWS ONLY
END
