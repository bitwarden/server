CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_UpdateIfUnchanged]
    @Id                         UNIQUEIDENTIFIER,
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
    @RevisionDate               DATETIME2(7),
    @ExpectedRevisionDate       DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPartnershipEntitlement]
    SET
        [OrganizationPartnershipId] = @OrganizationPartnershipId,
        [ExternalId] = @ExternalId,
        [ExternalIdHash] = @ExternalIdHash,
        [State] = @State,
        [UserId] = @UserId,
        [AccountRef] = @AccountRef,
        [Metadata] = @Metadata,
        [BoundDate] = @BoundDate,
        [SuspendedDate] = @SuspendedDate,
        [CanceledDate] = @CanceledDate,
        [ResumeWindowExpirationDate] = @ResumeWindowExpirationDate,
        [LastAppliedEffectiveDate] = @LastAppliedEffectiveDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [RevisionDate] = @ExpectedRevisionDate

    SELECT @@ROWCOUNT
END
