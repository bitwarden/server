CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ClearDisabledByIntegrationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Scoped through the integration so the write cannot cross tenants, matching the disable path
    UPDATE
        oic
    SET
        oic.[DisabledDate] = NULL,
        oic.[DisabledReason] = NULL,
        oic.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
    WHERE
        oic.[OrganizationIntegrationId] = @OrganizationIntegrationId
        AND oic.[DisabledDate] IS NOT NULL
        AND oi.[OrganizationId] = @OrganizationId

    -- Returned explicitly because SET NOCOUNT ON suppresses the row count ExecuteNonQuery would report
    SELECT @@ROWCOUNT
END
