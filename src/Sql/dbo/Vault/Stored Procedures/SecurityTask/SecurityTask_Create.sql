CREATE PROCEDURE [dbo].[SecurityTask_Create]
	@Id UNIQUEIDENTIFIER OUTPUT,
	@OrganizationId UNIQUEIDENTIFIER,
	@CipherId UNIQUEIDENTIFIER,
	@Type TINYINT,
	@Status TINYINT,
	@CreationDate DATETIME2(7),
	@RevisionDate DATETIME2(7)
AS
BEGIN
	SET NOCOUNT ON

	INSERT INTO [dbo].[SecurityTask]
    (
		[Id],
		[OrganizationId],
		[CipherId],
		[Type],
		[Status],
		[CreationDate],
		[RevisionDate]
	)
    VALUES
    (
		@Id,
		@OrganizationId,
		@CipherId,
		@Type,
		@Status,
		@CreationDate,
		@RevisionDate
	)
END
