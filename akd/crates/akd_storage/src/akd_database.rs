use std::collections::HashMap;

use akd::{
    errors::StorageError,
    storage::{
        types::{DbRecord, KeyData, ValueState, ValueStateRetrievalFlag},
        Database, DbSetState, Storable,
    },
    AkdLabel, AkdValue,
};
use async_trait::async_trait;

use crate::{
    db_config::DatabaseType, vrf_key_config::VrfKeyConfig, vrf_key_database::VrfKeyDatabase,
};

#[derive(Debug, Clone)]
pub struct AkdDatabase {
    db: DatabaseType,
    vrf_key_config: VrfKeyConfig,
}

impl AkdDatabase {
    pub fn db(&self) -> &DatabaseType {
        &self.db
    }

    pub fn new(db: DatabaseType, vrf_key_config: VrfKeyConfig) -> AkdDatabase {
        AkdDatabase { db, vrf_key_config }
    }

    pub fn vrf_key_database(&self) -> VrfKeyDatabase {
        VrfKeyDatabase::new(self.db.clone(), self.vrf_key_config.clone())
    }
}

#[async_trait]
impl Database for AkdDatabase {
    async fn set(&self, record: DbRecord) -> Result<(), StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.set(record).await,
        }
    }

    async fn batch_set(
        &self,
        records: Vec<DbRecord>,
        state: DbSetState, // TODO: unused in mysql example, but may be needed later
    ) -> Result<(), StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.batch_set(records, state).await,
        }
    }

    async fn get<St: Storable>(&self, id: &St::StorageKey) -> Result<DbRecord, StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.get::<St>(id).await,
        }
    }

    async fn batch_get<St: Storable>(
        &self,
        ids: &[St::StorageKey],
    ) -> Result<Vec<DbRecord>, StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.batch_get::<St>(ids).await,
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    async fn get_user_data(&self, raw_label: &AkdLabel) -> Result<KeyData, StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.get_user_data(raw_label).await,
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    async fn get_user_state(
        &self,
        raw_label: &AkdLabel,
        flag: ValueStateRetrievalFlag,
    ) -> Result<ValueState, StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.get_user_state(raw_label, flag).await,
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    async fn get_user_state_versions(
        &self,
        raw_labels: &[AkdLabel],
        flag: ValueStateRetrievalFlag,
    ) -> Result<HashMap<AkdLabel, (u64, AkdValue)>, StorageError> {
        match &self.db {
            DatabaseType::MsSql(db) => db.get_user_state_versions(raw_labels, flag).await,
        }
    }
}
