CREATE PROCEDURE [dbo].[OrganizationSubscriptionUpdate_UpdateSubscriptionStatus]
    @SuccessfulOrganizations NVARCHAR(MAX),
    @FailedOrganizations NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @SuccessfulOrgIds TABLE (Id UNIQUEIDENTIFIER)
    DECLARE @FailedOrgIds TABLE (Id UNIQUEIDENTIFIER)

    INSERT INTO @SuccessfulOrgIds (Id)
    SELECT [value] FROM OPENJSON(@SuccessfulOrganizations)

    INSERT INTO @FailedOrgIds (Id)
    SELECT [value] FROM OPENJSON(@FailedOrganizations)

    ;WITH OrgActions AS (
        -- Failed orgs take precedence
        SELECT Id, 'Failed' AS Action FROM @FailedOrgIds
        UNION ALL
        -- Successful orgs only if not in failed list
        SELECT Id, 'Successful' AS Action FROM @SuccessfulOrgIds
        WHERE Id NOT IN (SELECT Id FROM @FailedOrgIds)
    )
     UPDATE osu
     SET
         [SeatsLastUpdated] = CASE
                                  WHEN oa.Action = 'Successful' THEN NULL
                                  ELSE osu.[SeatsLastUpdated]
             END,
         [SyncAttempts] = CASE
                              WHEN oa.Action = 'Failed' THEN osu.[SyncAttempts] + 1
                              WHEN oa.Action = 'Successful' THEN 0
             END
     FROM [dbo].[OrganizationSubscriptionUpdate] osu
         INNER JOIN OrgActions oa ON osu.OrganizationId = oa.Id
END
