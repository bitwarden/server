CREATE PROCEDURE [dbo].[Organization_UnassignedToProviderSearch]
    @Name NVARCHAR(55),
    @OwnerEmail NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @NameLikeSearch NVARCHAR(55) = '%' + @Name + '%'
    DECLARE @OwnerLikeSearch NVARCHAR(55) = @OwnerEmail + '%'

    IF @OwnerEmail IS NOT NULL
    BEGIN
        SELECT
            O.*
        FROM
            [dbo].[OrganizationView] O
            INNER JOIN
                [dbo].[OrganizationUser] OU ON O.[Id] = OU.[OrganizationId]
            INNER JOIN
                [dbo].[User] U ON U.[Id] = OU.[UserId]
        WHERE
            O.[PlanType] NOT IN (0, 1, 6, 7) -- Not 'Free', 'Custom' or 'Families'
            AND NOT EXISTS (SELECT * FROM [dbo].[ProviderOrganizationView] PO WHERE PO.[OrganizationId] = O.[Id])
            AND (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
            AND (U.[Email] LIKE @OwnerLikeSearch)
        ORDER BY O.[CreationDate] DESC, O.[Id]
        OFFSET @Skip ROWS
        FETCH NEXT @Take ROWS ONLY
    END
    ELSE
    BEGIN
        SELECT
            O.*
        FROM
            [dbo].[OrganizationView] O
        WHERE
            O.[PlanType] NOT IN (0, 1, 6, 7) -- Not 'Free', 'Custom' or 'Families'
            AND NOT EXISTS (SELECT * FROM [dbo].[ProviderOrganizationView] PO WHERE PO.[OrganizationId] = O.[Id])
            AND (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
        ORDER BY O.[CreationDate] DESC, O.[Id]
        OFFSET @Skip ROWS
        FETCH NEXT @Take ROWS ONLY
    END
END
