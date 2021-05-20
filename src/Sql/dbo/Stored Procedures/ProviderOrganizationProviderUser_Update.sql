CREATE PROCEDURE [dbo].[ProviderOrganizationProviderUser_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderOrganizationId UNIQUEIDENTIFIER,
    @ProviderUserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Permissions NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ProviderOrganizationProviderUser]
    SET
        [ProviderOrganizationId] = @ProviderOrganizationId,
        [ProviderUserId] = @ProviderUserId,
        [Type] = @Type,
        [Permissions] = @Permissions,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderUserId] @ProviderUserId
END
