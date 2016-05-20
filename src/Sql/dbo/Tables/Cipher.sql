CREATE TABLE [dbo].[Cipher] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [FolderId]     UNIQUEIDENTIFIER NULL,
    [Type]         TINYINT          NOT NULL,
    [Data]         NVARCHAR (MAX)   NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Cipher] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Cipher_Folder] FOREIGN KEY ([FolderId]) REFERENCES [dbo].[Cipher] ([Id]),
    CONSTRAINT [FK_Cipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId]
    ON [dbo].[Cipher]([UserId] ASC);


GO
CREATE TRIGGER [dbo].[Cipher_Inserted]
ON [dbo].[Cipher] AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON
 
    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT [UserId] FROM INSERTED)
    DECLARE @CipherId UNIQUEIDENTIFIER = (SELECT [Id] FROM INSERTED)
    DECLARE @Date DATETIME2(7) = (SELECT [CreationDate] FROM INSERTED)
 
    EXEC [dbo].[History_Create] @UserId, @CipherId, 0 /* Insert */, @Date
END


GO
CREATE TRIGGER [dbo].[Cipher_Updated]
ON [dbo].[Cipher] AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON
 
    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT [UserId] FROM INSERTED)
    DECLARE @CipherId UNIQUEIDENTIFIER = (SELECT [Id] FROM INSERTED)
    DECLARE @Date DATETIME2(7) = (SELECT [RevisionDate] FROM INSERTED)
 
    EXEC [dbo].[History_Create] @UserId, @CipherId, 1 /* Update */, @Date
END


GO
CREATE TRIGGER [dbo].[Cipher_Deleted]
ON [dbo].[Cipher] AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON
 
    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT [UserId] FROM DELETED)
    DECLARE @CipherId UNIQUEIDENTIFIER = (SELECT [Id] FROM DELETED)
 
    EXEC [dbo].[History_Create] @UserId, @CipherId, 2 /* Delete */, GETUTCDATE
END
