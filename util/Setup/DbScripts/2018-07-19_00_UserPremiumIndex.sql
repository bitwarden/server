IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_User_Premium_PremiumExpirationDate_RenewalReminderDate'
    AND object_id = OBJECT_ID('[dbo].[User]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_Premium_PremiumExpirationDate_RenewalReminderDate]
        ON [dbo].[User]([Premium] ASC, [PremiumExpirationDate] ASC, [RenewalReminderDate] ASC)
END
GO
