CREATE TYPE [dbo].[SelectionReadOnlyArray] AS TABLE (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]      BIT              NOT NULL,
    [HidePasswords] BIT              NOT NULL);

