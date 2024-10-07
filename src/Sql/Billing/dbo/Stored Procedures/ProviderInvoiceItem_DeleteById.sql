CREATE PROCEDURE [dbo].[ProviderInvoiceItem_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ProviderInvoiceItem]
    WHERE
        [Id] = @Id
END
