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