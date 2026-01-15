use akd::{AkdLabel, AkdValue};
use async_trait::async_trait;
use thiserror::Error;
use uuid::Uuid;

use crate::{
    db_config::DatabaseType, ms_sql::MsSql, publish_queue_config::PublishQueueConfig, AkdDatabase,
};

#[derive(Debug, Error)]
#[error("Publish queue error")]
pub struct PublishQueueError;

#[async_trait]
pub trait PublishQueue {
    // TODO: should this method ensure that a given label is not already present in the queue? How to handle that?
    async fn enqueue(
        &self,
        raw_label: AkdLabel,
        raw_value: AkdValue,
    ) -> Result<(), PublishQueueError>;
    async fn peek(
        &self,
        limit: Option<isize>,
    ) -> Result<Vec<(Uuid, (AkdLabel, AkdValue))>, PublishQueueError>;
    async fn remove(&self, ids: Vec<uuid::Uuid>) -> Result<(), PublishQueueError>;
}

#[async_trait]
pub trait ReadOnlyPublishQueue {
    async fn label_pending_publish(&self, label: &AkdLabel) -> Result<bool, PublishQueueError>;
}

#[derive(Debug, Clone)]
pub enum PublishQueueType {
    MsSql(MsSql),
}

impl PublishQueueType {
    pub fn new(config: &PublishQueueConfig, db: &AkdDatabase) -> PublishQueueType {
        match config {
            PublishQueueConfig::DbBacked => db.into(),
        }
    }
}

impl From<&AkdDatabase> for PublishQueueType {
    fn from(db: &AkdDatabase) -> Self {
        match db.db() {
            DatabaseType::MsSql(ms_sql) => PublishQueueType::MsSql(ms_sql.clone()),
        }
    }
}

#[async_trait]
impl PublishQueue for PublishQueueType {
    async fn enqueue(
        &self,
        raw_label: AkdLabel,
        raw_value: AkdValue,
    ) -> Result<(), PublishQueueError> {
        match self {
            PublishQueueType::MsSql(ms_sql) => ms_sql.enqueue(raw_label, raw_value).await,
        }
    }

    async fn peek(
        &self,
        limit: Option<isize>,
    ) -> Result<Vec<(Uuid, (AkdLabel, AkdValue))>, PublishQueueError> {
        match self {
            PublishQueueType::MsSql(ms_sql) => ms_sql.peek(limit).await,
        }
    }

    async fn remove(&self, ids: Vec<uuid::Uuid>) -> Result<(), PublishQueueError> {
        match self {
            PublishQueueType::MsSql(ms_sql) => ms_sql.remove(ids).await,
        }
    }
}

#[derive(Debug, Clone)]
pub enum ReadOnlyPublishQueueType {
    MsSql(MsSql),
}

impl ReadOnlyPublishQueueType {
    pub fn new(config: &PublishQueueConfig, db: &AkdDatabase) -> ReadOnlyPublishQueueType {
        match config {
            PublishQueueConfig::DbBacked => db.into(),
        }
    }
}

impl From<&AkdDatabase> for ReadOnlyPublishQueueType {
    fn from(db: &AkdDatabase) -> Self {
        match db.db() {
            DatabaseType::MsSql(ms_sql) => ReadOnlyPublishQueueType::MsSql(ms_sql.clone()),
        }
    }
}

#[async_trait]
impl ReadOnlyPublishQueue for ReadOnlyPublishQueueType {
    async fn label_pending_publish(&self, label: &AkdLabel) -> Result<bool, PublishQueueError> {
        match self {
            ReadOnlyPublishQueueType::MsSql(ms_sql) => ms_sql.label_pending_publish(label).await,
        }
    }
}
