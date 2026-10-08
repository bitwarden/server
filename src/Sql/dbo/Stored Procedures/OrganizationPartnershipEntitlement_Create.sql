CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_Create]
    @Id                         UNIQUEIDENTIFIER OUTPUT,
    @OrganizationPartnershipId  UNIQUEIDENTIFIER,
    @ExternalId                 NVARCHAR(MAX),
    @ExternalIdHash             VARCHAR(64),
    @State                      TINYINT,
    @UserId                     UNIQUEIDENTIFIER,
    @AccountRef                 UNIQUEIDENTIFIER,
    @Metadata                   NVARCHAR(MAX),
    @BoundDate                  DATETIME2(7),
    @SuspendedDate              DATETIME2(7),
    @CanceledDate               DATETIME2(7),
    @ResumeWindowExpirationDate DATETIME2(7),
    @LastAppliedEffectiveDate   DATETIME2(7),
    @CreationDate               DATETIME2(7),
    @RevisionDate               DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPartnershipEntitlement]
    (
        [Id],
        [OrganizationPartnershipId],
        [ExternalId],
        [ExternalIdHash],
        [State],
        [UserId],
        [AccountRef],
        [Metadata],
        [BoundDate],
        [SuspendedDate],
        [CanceledDate],
        [ResumeWindowExpirationDate],
        [LastAppliedEffectiveDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationPartnershipId,
        @ExternalId,
        @ExternalIdHash,
        @State,
        @UserId,
        @AccountRef,
        @Metadata,
        @BoundDate,
        @SuspendedDate,
        @CanceledDate,
        @ResumeWindowExpirationDate,
        @LastAppliedEffectiveDate,
        @CreationDate,
        @RevisionDate
    )
END
