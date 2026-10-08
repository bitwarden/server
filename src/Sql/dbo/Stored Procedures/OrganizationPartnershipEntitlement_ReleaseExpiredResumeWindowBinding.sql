CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReleaseExpiredResumeWindowBinding]
    @Id UNIQUEIDENTIFIER,
    @AsOf DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- State 3 is PartnershipEntitlementState.Canceled. Only these two columns change, and only if the row still
    -- qualifies, so a concurrent re-provision or resume is never overwritten.
    UPDATE
        [dbo].[OrganizationPartnershipEntitlement]
    SET
        [UserId] = NULL,
        [AccountRef] = NULL,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [State] = 3
        AND [UserId] IS NOT NULL
        AND [ResumeWindowExpirationDate] <= @AsOf

    SELECT @@ROWCOUNT
END
