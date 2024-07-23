CREATE TABLE [dbo].[Cache]
(
    [Id] NVARCHAR (449) NOT NULL,
    [Value] VARBINARY (MAX) NOT NULL,
    [ExpiresAtTime] DATETIMEOFFSET (7) NOT NULL,
    [SlidingExpirationInSeconds] BIGINT NULL,
    [AbsoluteExpiration] DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_Cache] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_Cache_ExpiresAtTime]
    ON [dbo].[Cache]([ExpiresAtTime] ASC);
GO
