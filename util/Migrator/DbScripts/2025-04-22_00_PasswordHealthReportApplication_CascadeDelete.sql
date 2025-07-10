BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PasswordHealthReportApplication_Organization')
    BEGIN
        ALTER TABLE [dbo].[PasswordHealthReportApplication]
          DROP CONSTRAINT [FK_PasswordHealthReportApplication_Organization]
    END

    ALTER TABLE [dbo].[PasswordHealthReportApplication]
        ADD CONSTRAINT [FK_PasswordHealthReportApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
END
GO
