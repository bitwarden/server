CREATE PROCEDURE [dbo].[OrganizationIntegration_Disable]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @DisabledDate DATETIME2(7),
    @DisabledReason INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Only the first caller wins, so concurrent trips across instances collapse into a single write
    UPDATE
        [dbo].[OrganizationIntegration]
    SET
        [DisabledDate] = @DisabledDate,
        [DisabledReason] = @DisabledReason,
        [RevisionDate] = @RevisionDate
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Type] = @Type
        AND [DisabledDate] IS NULL

    -- Returned explicitly because SET NOCOUNT ON suppresses the row count ExecuteNonQuery would report
    SELECT @@ROWCOUNT
END
