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

    -- Insert
    INSERT INTO
        [dbo].[ProviderOrganization]
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
END