-- Security Tasks

-- Table
IF OBJECT_ID('[dbo].[SecurityTask]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[SecurityTask]
        (
            [Id] UNIQUEIDENTIFIER NOT NULL,
            [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
            [CipherId] UNIQUEIDENTIFIER NULL,
            [Type] TINYINT NOT NULL,
            [Status] TINYINT NOT NULL,
            [CreationDate] DATETIME2 (7) NOT NULL,
            [RevisionDate] DATETIME2 (7) NOT NULL,
            CONSTRAINT [PK_SecurityTask] PRIMARY KEY CLUSTERED ([Id] ASC),
            CONSTRAINT [FK_SecurityTask_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
            CONSTRAINT [FK_SecurityTask_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE,
            );

        CREATE NONCLUSTERED INDEX [IX_SecurityTask_CipherId]
            ON [dbo].[SecurityTask]([CipherId] ASC) WHERE CipherId IS NOT NULL;

        CREATE NONCLUSTERED INDEX [IX_SecurityTask_OrganizationId]
            ON [dbo].[SecurityTask]([OrganizationId] ASC) WHERE OrganizationId IS NOT NULL;
    END
GO

-- View SecurityTask
CREATE OR ALTER VIEW [dbo].[SecurityTaskView]
AS
SELECT
    *
FROM
    [dbo].[SecurityTask]
GO

-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_Create]
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
GO

-- Stored Procedures: Update
CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_Update]
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
GO

-- Stored Procedures: ReadById
CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SecurityTaskView]
    WHERE
        [Id] = @Id
END
GO
