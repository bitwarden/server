CREATE PROCEDURE [dbo].[SecurityTask_DeleteById]
	@Id UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON

DELETE FROM
    [dbo].[SecurityTask]
WHERE
    [Id] = @Id
END
