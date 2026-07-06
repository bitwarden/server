CREATE PROCEDURE [dbo].[OrganizationInviteLink_Create]
    @Id                   UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationInviteLink]
    (
        [Id],
        [Code],
        [OrganizationId],
        [AllowedDomains],
        [Invite],
        [SupportsConfirmation],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Code,
        @OrganizationId,
        @AllowedDomains,
        @Invite,
        @SupportsConfirmation,
        @CreationDate,
        @RevisionDate
    )
END
