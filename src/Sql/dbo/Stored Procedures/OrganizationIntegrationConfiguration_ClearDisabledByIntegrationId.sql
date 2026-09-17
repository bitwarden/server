CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ClearDisabledByIntegrationId]
    @OrganizationIntegrationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegrationConfiguration]
    SET
        [DisabledDate] = NULL,
        [DisabledReason] = NULL
    WHERE
        [OrganizationIntegrationId] = @OrganizationIntegrationId
        AND [DisabledDate] IS NOT NULL
END
