CREATE PROCEDURE [dbo].[OrganizationInviteLink_Create]
    @Id                 UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationInviteLink]
    (
        [Id],
        [Code],
        [OrganizationId],
        [AllowedDomains],
        [EncryptedInviteKey],
        [EncryptedOrgKey],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Code,
        @OrganizationId,
        @AllowedDomains,
        @EncryptedInviteKey,
        @EncryptedOrgKey,
        @CreationDate,
        @RevisionDate
    )
END
