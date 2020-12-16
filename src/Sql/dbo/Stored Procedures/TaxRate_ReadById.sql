CREATE PROCEDURE [dbo].[TaxRate_ReadById]
    @Id VARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Id = @Id
END