IF OBJECT_ID('dbo.akd_publish_queue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.akd_publish_queue (
        id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        raw_label VARBINARY(256) NOT NULL UNIQUE,
        raw_value VARBINARY(2000) NULL,
    );

    CREATE UNIQUE INDEX IX_akd_publish_queue_raw_label
    ON dbo.akd_publish_queue(raw_label);
END
