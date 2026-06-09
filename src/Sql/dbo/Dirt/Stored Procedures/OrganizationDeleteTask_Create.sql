CREATE PROCEDURE [dbo].[OrganizationDeleteTask_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @TaskType TINYINT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationDeleteTask]
    (
        [Id],
        [OrganizationId],
        [TaskType],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @TaskType,
        @CreationDate
    )
END
