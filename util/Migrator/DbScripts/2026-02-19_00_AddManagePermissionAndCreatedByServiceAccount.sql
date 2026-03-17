-- Add Manage column to AccessPolicy and CreatedByServiceAccountId to Project

-- Step 1: Add the column first (separate batch to avoid compile-time "invalid column" on subsequent DML)
IF OBJECT_ID('[dbo].[AccessPolicy]') IS NOT NULL AND COL_LENGTH('[dbo].[AccessPolicy]', 'Manage') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessPolicy]
        ADD [Manage] BIT NOT NULL DEFAULT (0);
END
GO

-- Step 2: Backfill and add constraints (compiled after column exists)
IF OBJECT_ID('[dbo].[AccessPolicy]') IS NOT NULL AND COL_LENGTH('[dbo].[AccessPolicy]', 'Manage') IS NOT NULL
BEGIN
    -- Backfill: preserve current behavior — user and group policies with Write=1 get Manage=1
    -- Service account policies remain Manage=0 per spec
    UPDATE [dbo].[AccessPolicy]
    SET [Manage] = 1
    WHERE [Write] = 1
      AND [Discriminator] IN (
        'user_project',
        'user_service_account',
        'user_secret',
        'group_project',
        'group_service_account',
        'group_secret'
      );

    -- Enforce permission hierarchy at DB level
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AccessPolicy_ManageImpliesWrite' AND parent_object_id = OBJECT_ID('[dbo].[AccessPolicy]'))
        ALTER TABLE [dbo].[AccessPolicy]
            ADD CONSTRAINT [CK_AccessPolicy_ManageImpliesWrite] CHECK ([Manage] = 0 OR [Write] = 1);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AccessPolicy_WriteImpliesRead' AND parent_object_id = OBJECT_ID('[dbo].[AccessPolicy]'))
        ALTER TABLE [dbo].[AccessPolicy]
            ADD CONSTRAINT [CK_AccessPolicy_WriteImpliesRead] CHECK ([Write] = 0 OR [Read] = 1);
END
GO

IF OBJECT_ID('[dbo].[Project]') IS NOT NULL AND COL_LENGTH('[dbo].[Project]', 'CreatedByServiceAccountId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Project]
        ADD [CreatedByServiceAccountId] UNIQUEIDENTIFIER NULL;

    CREATE INDEX [IX_Project_CreatedByServiceAccountId]
        ON [dbo].[Project] ([CreatedByServiceAccountId]);

    ALTER TABLE [dbo].[Project]
        ADD CONSTRAINT [FK_Project_ServiceAccount_CreatedByServiceAccountId]
        FOREIGN KEY ([CreatedByServiceAccountId])
        REFERENCES [dbo].[ServiceAccount] ([Id])
        ON DELETE SET NULL;
END
GO
