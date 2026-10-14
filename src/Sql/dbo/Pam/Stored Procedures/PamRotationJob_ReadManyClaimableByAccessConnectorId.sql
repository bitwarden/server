CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyClaimableByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The access connector poll; re-derives PamRotationJob_UpdateClaim's eligibility so the poll list matches what's
    -- claimable.
    SELECT
        J.*,
        C.[TargetSystemId]
    FROM
        [dbo].[PamRotationJobView] J
    INNER JOIN
        [dbo].[PamRotationConfigView] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN
        [dbo].[PamTargetSystemView] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN
        [dbo].[PamAccessConnectorTargetAssignmentView] A ON A.[AccessConnectorId] = @AccessConnectorId
            AND A.[TargetSystemId] = C.[TargetSystemId]
    INNER JOIN
        [dbo].[PamAccessConnectorView] AC ON AC.[Id] = @AccessConnectorId
            AND AC.[OrganizationId] = C.[OrganizationId]
            AND AC.[Status] = 0 -- Enabled
    WHERE
        J.[Action] = 0 -- None
        AND J.[ExpirationDate] > @Now
        AND J.[NextClaimableDate] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active
END
