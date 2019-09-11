DROP TABLE IF EXISTS collection_cipher;

CREATE TABLE IF NOT EXISTS collection_cipher (
    collection_id UUID NOT NULL,
    cipher_id     UUID NOT NULL,
    CONSTRAINT pk_collection_cipher PRIMARY KEY (collection_id, cipher_id),
    CONSTRAINT fk_collection_cipher_cipher FOREIGN KEY (cipher_id) REFERENCES cipher (id) ON DELETE CASCADE,
    CONSTRAINT fk_collection_cipher_collection FOREIGN KEY (collection_id) REFERENCES collection (id) ON DELETE CASCADE
);



CREATE INDEX ix_collection_cipher_cipher_id
    ON collection_cipher(cipher_id ASC);

