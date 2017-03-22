CREATE TABLE [dbo].[Cipher] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [Type]           TINYINT          NOT NULL,
    [Data]           NVARCHAR (MAX)   NOT NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Cipher] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Cipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_Type]
    ON [dbo].[Cipher]([UserId] ASC, [Type] ASC);


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

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM INSERTED)

    UPDATE
        [User]
    SET
        [AccountRevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @UserId
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

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM INSERTED)

    UPDATE
        [User]
    SET
        [AccountRevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @UserId
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

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM DELETED)

    UPDATE
        [User]
    SET
        [AccountRevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @UserId
END
