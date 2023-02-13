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
    O.[PlanType] >= 8 AND O.[PlanType] <= 11 -- Get 'Team' and 'Enterprise' Organizations
    AND NOT EXISTS (SELECT * FROM [dbo].[ProviderOrganizationView] po WHERE po.[OrganizationId] = O.[Id])
    AND OU.[Type] = 0 --Get 'Owner' type users only
    AND (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
    AND (@OwnerEmail IS NULL OR U.[Email] = @OwnerEmail)
ORDER BY O.[CreationDate] DESC
OFFSET @Skip ROWS
FETCH NEXT @Take ROWS ONLY
END
GO

CREATE PROCEDURE [dbo].[ProviderOrganization_CreateWithManyOrganizations]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Key VARCHAR(MAX),
    @Settings NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON
        
    DECLARE @ProviderOrganizationsToInsert TABLE (
        [Id]             UNIQUEIDENTIFIER    NOT NULL,
        [ProviderId]     UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER    NULL,
        [Key]            VARCHAR (MAX)       NULL,
        [Settings]       NVARCHAR(MAX)       NULL,
        [CreationDate]   DATETIME2 (7)       NOT NULL,
        [RevisionDate]   DATETIME2 (7)       NOT NULL
    );

    -- Insert
    INSERT INTO
        @ProviderOrganizationsToInsert
    SELECT
        NEWID(),
        @ProviderId,
        [Source].[Id],
        @Key,
        @Settings,
        @CreationDate,
        @RevisionDate
    FROM
        @OrganizationIds AS [Source]
        INNER JOIN
            [dbo].[Organization] O ON [Source].[Id] = O.[Id]
    WHERE
        NOT EXISTS (
        SELECT
            1
        FROM
            [dbo].[ProviderOrganization]
        WHERE
            [ProviderId] = @ProviderId
            AND [OrganizationId] = [Source].[Id]
        )

    INSERT INTO [dbo].[ProviderOrganization] ([Id], [ProviderId], [OrganizationId], [Key], [Settings], [CreationDate], [RevisionDate])
    SELECT      [Id], [ProviderId], [OrganizationId], [Key], [Settings], [CreationDate], [RevisionDate]
    FROM        @ProviderOrganizationsToInsert

    SELECT * FROM @ProviderOrganizationsToInsert
END
GO