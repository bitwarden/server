CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_StartIfPending]
    @Id           UNIQUEIDENTIFIER,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Status 0 is Pending and 1 is Running
    UPDATE
        [dbo].[OrganizationPartnershipReconciliationExport]
    SET
        [Status] = 1,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [Status] = 0

    SELECT @@ROWCOUNT
END
