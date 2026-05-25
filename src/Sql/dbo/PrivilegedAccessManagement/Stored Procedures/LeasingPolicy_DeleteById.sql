CREATE PROCEDURE [dbo].[LeasingPolicy_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[LeasingPolicy] WHERE [Id] = @Id
END
