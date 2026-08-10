CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadCount]
AS
BEGIN
    SET NOCOUNT ON

    -- One-time anchor for the protection migration's pending-rows metric and partition split.
    SELECT COUNT_BIG(*)
    FROM [dbo].[OrganizationApiKey]
END
