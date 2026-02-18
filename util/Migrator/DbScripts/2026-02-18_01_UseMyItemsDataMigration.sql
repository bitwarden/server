-- Enable UseMyItems for all organizations with UsePolicies enabled
UPDATE [dbo].[Organization]
SET [UseMyItems] = 1
WHERE [UsePolicies] = 1;
