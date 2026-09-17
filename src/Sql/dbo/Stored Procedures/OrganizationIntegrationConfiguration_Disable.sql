CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Disable]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER,
    @DisabledDate DATETIME2(7),
    @DisabledReason INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Scoped through the integration so the write cannot cross tenants. Only the first caller wins, so concurrent
    -- trips across instances collapse into a single write.
    UPDATE
        oic
    SET
        oic.[DisabledDate] = @DisabledDate,
        oic.[DisabledReason] = @DisabledReason,
        oic.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
    WHERE
        oic.[Id] = @Id
        AND oic.[DisabledDate] IS NULL
        AND oi.[OrganizationId] = @OrganizationId

    -- Returned explicitly because SET NOCOUNT ON suppresses the row count ExecuteNonQuery would report
    SELECT @@ROWCOUNT
END
