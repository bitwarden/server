IF OBJECT_ID('dbo.akd_values', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.akd_values (
        raw_label VARBINARY(256) NOT NULL,
        epoch BIGINT NOT NULL CHECK (epoch >= 0),
        [version] BIGINT NOT NULL CHECK ([version] >= 0),
        node_label_val VARBINARY(32) NOT NULL,
        node_label_len INT NOT NULL CHECK (node_label_len >= 0),
        [data] VARBINARY(2000) NULL,
        PRIMARY KEY (raw_label, epoch)
    );
END
