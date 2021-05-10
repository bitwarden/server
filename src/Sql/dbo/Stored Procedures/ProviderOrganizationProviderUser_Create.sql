CREATE PROCEDURE [dbo].[ProviderOrganizationProviderUser_Create]
    @Id UNIQUEIDENTIFIER,
    @ProviderOrganizationId UNIQUEIDENTIFIER,
    @ProviderUserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderOrganizationProviderUser]
    (
        [Id],
        [ProviderOrganizationId],
        [ProviderUserId],
        [Type],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @ProviderOrganizationId,
        @ProviderUserId,
        @Type,
        @CreationDate,
        @RevisionDate
    )
END
