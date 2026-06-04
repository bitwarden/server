CREATE TYPE [dbo].[CollectionAccessSelectionType] AS TABLE (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]      BIT              NOT NULL,
    [HidePasswords] BIT              NOT NULL,
    [Manage]        BIT              NOT NULL);

