use async_trait::async_trait;
use thiserror::Error;

use crate::ms_sql::MsSql;

pub(crate) struct PublishQueueItem {
    pub id: uuid::Uuid,
    pub raw_label: Vec<u8>,
    pub raw_value: Vec<u8>,
}

#[derive(Debug, Error)]
#[error("Publish queue error")]
pub struct PublishQueueError;

#[async_trait]
pub trait PublishQueue {
    async fn enqueue(
        &self,
        raw_label: Vec<u8>,
        raw_value: Vec<u8>,
    ) -> Result<(), PublishQueueError>;
    async fn peek(&self, limit: isize) -> Result<Vec<PublishQueueItem>, PublishQueueError>;
    async fn remove(&self, ids: Vec<uuid::Uuid>) -> Result<(), PublishQueueError>;
}

#[derive(Debug, Clone)]
pub enum PublishQueueType {
    MsSql(MsSql),
}

#[async_trait]
impl PublishQueue for PublishQueueType {
    async fn enqueue(
        &self,
        _raw_label: Vec<u8>,
        _raw_value: Vec<u8>,
    ) -> Result<(), PublishQueueError> {
        match self {
            PublishQueueType::MsSql(_ms_sql) => {
                // Implement enqueue logic for MsSql
                Ok(())
            }
        }
    }

    async fn peek(&self, _max: isize) -> Result<Vec<PublishQueueItem>, PublishQueueError> {
        match self {
            PublishQueueType::MsSql(_ms_sql) => {
                // Implement peek logic for MsSql
                Ok(vec![])
            }
        }
    }

    async fn remove(&self, _ids: Vec<uuid::Uuid>) -> Result<(), PublishQueueError> {
        match self {
            PublishQueueType::MsSql(_ms_sql) => {
                // Implement remove logic for MsSql
                Ok(())
            }
        }
    }
}
