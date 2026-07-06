CREATE PROCEDURE [dbo].[OrganizationInviteLink_Update]
    @Id                   UNIQUEIDENTIFIER,
    @Code                 UNIQUEIDENTIFIER,
    @OrganizationId       UNIQUEIDENTIFIER,
    @AllowedDomains       NVARCHAR(MAX),
    @Invite               NVARCHAR(MAX),
    @SupportsConfirmation BIT,
    @CreationDate         DATETIME2(7),
    @RevisionDate         DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationInviteLink]
    SET
        [Code] = @Code,
        [OrganizationId] = @OrganizationId,
        [AllowedDomains] = @AllowedDomains,
        [Invite] = @Invite,
        [SupportsConfirmation] = @SupportsConfirmation,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
