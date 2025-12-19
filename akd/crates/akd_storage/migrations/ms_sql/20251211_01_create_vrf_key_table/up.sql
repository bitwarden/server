IF OBJECT_ID('dbo.akd_vrf_keys', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.akd_vrf_keys (
        root_key_hash VARBINARY(32) NOT NULL,
        root_key_type SMALLINT NOT NULL,
        enc_sym_key VARBINARY(max) NULL,
        sym_enc_vrf_key VARBINARY(48) NOT NULL,
        sym_enc_vrf_key_nonce VARBINARY(24) NULL,
        PRIMARY KEY (root_key_hash, root_key_type)
    );
END
