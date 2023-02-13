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