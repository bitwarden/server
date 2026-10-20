CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadPageByOrganizationPartnershipIdRevisionDate]
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @State                     TINYINT,
    @RevisedAfter              DATETIME2(7),
    @AfterRevisionDate         DATETIME2(7),
    @AfterId                   UNIQUEIDENTIFIER,
    @PageSize                  INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@PageSize)
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [OrganizationPartnershipId] = @OrganizationPartnershipId
        AND (@State IS NULL OR [State] = @State)
        AND (@RevisedAfter IS NULL OR [RevisionDate] > @RevisedAfter)
        AND (
            @AfterRevisionDate IS NULL
            OR [RevisionDate] > @AfterRevisionDate
            OR ([RevisionDate] = @AfterRevisionDate AND [Id] > @AfterId)
        )
    ORDER BY
        [RevisionDate] ASC,
        [Id] ASC
END
