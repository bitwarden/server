CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Disable]
    @Id UNIQUEIDENTIFIER,
    @DisabledDate DATETIME2(7),
    @DisabledReason INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Only the first caller wins, so concurrent trips across instances collapse into a single write
    UPDATE
        [dbo].[OrganizationIntegrationConfiguration]
    SET
        [DisabledDate] = @DisabledDate,
        [DisabledReason] = @DisabledReason,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [DisabledDate] IS NULL

    -- Returned explicitly because SET NOCOUNT ON suppresses the row count ExecuteNonQuery would report
    SELECT @@ROWCOUNT
END
