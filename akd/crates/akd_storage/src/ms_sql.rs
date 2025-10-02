use std::collections::HashMap;

use akd::{errors::StorageError, storage::{types::{self, DbRecord}, Database, DbSetState, Storable}, AkdLabel, AkdValue};
use async_trait::async_trait;
use ms_database::ConnectionManager;

pub struct MsSql {
    connection_manager: ConnectionManager,
}

#[async_trait]
impl Database for MsSql {
    async fn set(&self, record: DbRecord) -> Result<(), StorageError> {
        todo!()
    }

    async fn batch_set(&self, records: Vec<DbRecord>, state: DbSetState) -> Result<(), StorageError> {
        todo!()
    }

    async fn get<St: Storable>(&self, id: &St::StorageKey) -> Result<DbRecord, StorageError> {
        todo!()
    }

    async fn batch_get<St: Storable>(&self, ids: &[St::StorageKey]) -> Result<Vec<DbRecord>, StorageError> {
        todo!()
    }

    async fn get_user_data(&self, username: &AkdLabel) -> Result<types::KeyData, StorageError> {
        todo!()
    }

    async fn get_user_state(
        &self,
        username: &AkdLabel,
        flag: types::ValueStateRetrievalFlag,
    ) -> Result<types::ValueState, StorageError> {
        todo!()
    }

    async fn get_user_state_versions(
        &self,
        usernames: &[AkdLabel],
        flag: types::ValueStateRetrievalFlag,
    ) -> Result<HashMap<AkdLabel, (u64, AkdValue)>, StorageError> {
        todo!()
    }
}
