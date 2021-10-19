CREATE PROCEDURE [dbo].[TaxRate_Create]
    @Id VARCHAR(40) OUTPUT,
    @Country VARCHAR(50),
    @State VARCHAR(2),
    @PostalCode VARCHAR(10),
    @Rate DECIMAL(6,3),
    @Active BIT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[TaxRate]
    (
        [Id],
        [Country],
        [State],
        [PostalCode],
        [Rate],
        [Active]
    )
    VALUES
    (
        @Id,
        @Country,
        @State,
        @PostalCode,
        @Rate,
        1
    )
END
