-- Create TwoGuidIdArray Type
IF NOT EXISTS (
    SELECT 
        *
    FROM 
        sys.types
    WHERE 
        [Name] = 'TwoGuidIdArray' AND
        is_user_defined = 1
)
CREATE TYPE [dbo].[TwoGuidIdArray] AS TABLE (
    [Id1] UNIQUEIDENTIFIER NOT NULL,
    [Id2] UNIQUEIDENTIFIER NOT NULL);
GO
