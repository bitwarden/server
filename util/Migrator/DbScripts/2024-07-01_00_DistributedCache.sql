IF OBJECT_ID('[dbo].[Cache]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Cache] (
        [Id] [nvarchar](449) NOT NULL,
        [Value] [varbinary](max) NOT NULL,
        [ExpiresAtTime] [datetimeoffset](7) NOT NULL,
        [SlidingExpirationInSeconds] [bigint] NULL,
        [AbsoluteExpiration] [datetimeoffset](7) NULL,
        CONSTRAINT [PK_Cache] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_Cache_ExpiresAtTime]
        ON [dbo].[Cache]([ExpiresAtTime] ASC);
END
GO
