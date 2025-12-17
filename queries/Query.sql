select * from OrganizationReport;

INSERT INTO [dbo].[OrganizationReport] (
    [Id],
    [OrganizationId],
    [ReportData],
    [CreationDate],
    [ContentEncryptionKey],
    [SummaryData],
    [ApplicationData],
    [RevisionDate]
)
VALUES
('b1a1a1a1-1111-1111-1111-111111111111', '66cf63fb-2b01-4b5b-87b9-b344007cf40c', N'Random Report 1', SYSDATETIME(), 'Key1', N'Summary 1', N'AppData 1', SYSDATETIME()),
('b1a1a1a1-2222-1111-1111-111111111112', '66cf63fb-2b01-4b5b-87b9-b344007cf40c', N'Random Report 2', SYSDATETIME(), 'Key2', N'Summary 2', N'AppData 2', SYSDATETIME()),
('b1a1a1a1-3333-1111-1111-111111111113', 'c2b2b2b2-3333-2222-2222-222222222224', N'Random Report 3', SYSDATETIME(), 'Key3', N'Summary 3', N'AppData 3', SYSDATETIME()),
('b1a1a1a1-4444-1111-1111-111111111114', 'c2b2b2b2-4444-2222-2222-222222222225', N'Random Report 4', SYSDATETIME(), 'Key4', N'Summary 4', N'AppData 4', SYSDATETIME()),
('b1a1a1a1-5555-1111-1111-111111111115', 'c2b2b2b2-5555-2222-2222-222222222226', N'Random Report 5', SYSDATETIME(), 'Key5', N'Summary 5', N'AppData 5', SYSDATETIME()),
('b1a1a1a1-6666-1111-1111-111111111116', 'c2b2b2b2-6666-2222-2222-222222222227', N'Random Report 6', SYSDATETIME(), 'Key6', N'Summary 6', N'AppData 6', SYSDATETIME()),
('b1a1a1a1-7777-1111-1111-111111111117', 'c2b2b2b2-7777-2222-2222-222222222228', N'Random Report 7', SYSDATETIME(), 'Key7', N'Summary 7', N'AppData 7', SYSDATETIME()),
('b1a1a1a1-8888-1111-1111-111111111118', 'c2b2b2b2-8888-2222-2222-222222222229', N'Random Report 8', SYSDATETIME(), 'Key8', N'Summary 8', N'AppData 8', SYSDATETIME()),
('b1a1a1a1-9999-1111-1111-111111111119', 'c2b2b2b2-9999-2222-2222-222222222230', N'Random Report 9', SYSDATETIME(), 'Key9', N'Summary 9', N'AppData 9', SYSDATETIME()),
('b1a1a1a1-0000-1111-1111-111111111120', 'c2b2b2b2-0000-2222-2222-222222222231', N'Random Report 10', SYSDATETIME(), 'Key10', N'Summary 10', N'AppData 10', SYSDATETIME());