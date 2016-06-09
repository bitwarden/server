CREATE TABLE [dbo].[Cipher] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [FolderId]     UNIQUEIDENTIFIER NULL,
    [Type]         TINYINT          NOT NULL,
    [Favorite]     BIT              NOT NULL,
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
    DECLARE @Count INT = (SELECT COUNT(1) FROM INSERTED)
    IF @Count = 0 RETURN

    SET NOCOUNT ON

    INSERT INTO [dbo].[History]
    (
        [UserId],
        [CipherId],
        [Event],
        [Date]
    )
    SELECT
        [UserId],
        [Id],
        0, --Insert
        [CreationDate]
    FROM
        INSERTED
END

GO
CREATE TRIGGER [dbo].[Cipher_Updated]
ON [dbo].[Cipher] AFTER UPDATE
AS
BEGIN
    DECLARE @Count INT = (SELECT COUNT(1) FROM INSERTED)
    IF @Count = 0 RETURN

    SET NOCOUNT ON

    INSERT INTO [dbo].[History]
    (
        [UserId],
        [CipherId],
        [Event],
        [Date]
    )
    SELECT
        [UserId],
        [Id],
        1, --Update
        [RevisionDate]
    FROM
        INSERTED
END

GO
CREATE TRIGGER [dbo].[Cipher_Deleted]
ON [dbo].[Cipher] AFTER DELETE
AS
BEGIN
    DECLARE @Count INT = (SELECT COUNT(1) FROM DELETED)
    IF @Count = 0 RETURN

    SET NOCOUNT ON

    INSERT INTO [dbo].[History]
    (
        [UserId],
        [CipherId],
        [Event],
        [Date]
    )
    SELECT
        [UserId],
        [Id],
        2, --Delete
        GETUTCDATE()
    FROM
        DELETED
END
