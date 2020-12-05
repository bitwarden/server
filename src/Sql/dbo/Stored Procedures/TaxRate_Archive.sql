CREATE PROCEDURE [dbo].[TaxRate_Archive]
    @Id VARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[TaxRate]
    SET
        [Active] = 0
    WHERE
        [Id] = @Id
END
