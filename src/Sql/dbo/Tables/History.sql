CREATE TABLE [dbo].[History] (
    [Id]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [UserId]   UNIQUEIDENTIFIER NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    [Event]    TINYINT          NOT NULL,
    [Date]     DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_CipherHistory] PRIMARY KEY CLUSTERED ([Id] ASC)
);

