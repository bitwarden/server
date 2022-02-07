IF COL_LENGTH('[dbo].[Organization]', 'CloudBillingSyncKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Organization]
        ADD
            [CloudBillingSyncKey] VARCHAR(30) NULL
    END
GO
