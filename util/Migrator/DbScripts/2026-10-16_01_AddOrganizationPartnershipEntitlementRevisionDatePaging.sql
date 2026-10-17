IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId_RevisionDate_Id' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipEntitlement]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId_RevisionDate_Id]
        ON [dbo].[OrganizationPartnershipEntitlement]([OrganizationPartnershipId] ASC, [RevisionDate] ASC, [Id] ASC)
        INCLUDE ([State]);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadPageByOrganizationPartnershipIdRevisionDate]
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
GO
