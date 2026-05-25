CREATE PROCEDURE [dbo].[LeasingPolicy_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[LeasingPolicy]
    WHERE [Id] = @Id
END
