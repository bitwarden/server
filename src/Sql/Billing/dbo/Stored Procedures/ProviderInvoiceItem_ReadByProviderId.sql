CREATE PROCEDURE [dbo].[ProviderInvoiceItem_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderInvoiceItemView]
    WHERE
        [ProviderId] = @ProviderId
END
