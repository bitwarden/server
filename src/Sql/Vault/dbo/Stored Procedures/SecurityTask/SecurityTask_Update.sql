CREATE PROCEDURE [dbo].[SecurityTask_Update]
	@Id UNIQUEIDENTIFIER,
	@OrganizationId UNIQUEIDENTIFIER,
	@CipherId UNIQUEIDENTIFIER,
	@Type TINYINT,
	@Status TINYINT,
	@CreationDate DATETIME2(7),
	@RevisionDate DATETIME2(7)
AS
BEGIN
	SET NOCOUNT ON

	UPDATE
	    [dbo].[SecurityTask]
	SET
        [OrganizationId] = @OrganizationId,
		[CipherId] = @CipherId,
		[Type] = @Type,
		[Status] = @Status,
		[CreationDate] = @CreationDate,
		[RevisionDate] = @RevisionDate
	WHERE
        [Id] = @Id
END
