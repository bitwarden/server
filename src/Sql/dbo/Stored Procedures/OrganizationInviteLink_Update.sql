CREATE PROCEDURE [dbo].[OrganizationInviteLink_Update]
    @Id                 UNIQUEIDENTIFIER,
    @Code               UNIQUEIDENTIFIER,
    @OrganizationId     UNIQUEIDENTIFIER,
    @AllowedDomains     NVARCHAR(MAX),
    @EncryptedInviteKey NVARCHAR(MAX),
    @EncryptedOrgKey    NVARCHAR(MAX),
    @CreationDate       DATETIME2(7),
    @RevisionDate       DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationInviteLink]
    SET
        [Code] = @Code,
        [OrganizationId] = @OrganizationId,
        [AllowedDomains] = @AllowedDomains,
        [EncryptedInviteKey] = @EncryptedInviteKey,
        [EncryptedOrgKey] = @EncryptedOrgKey,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
