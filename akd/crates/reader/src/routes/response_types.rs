use crate::routes::Response;

pub type AuditResponse = Response<crate::routes::audit::AuditData>;
pub type BatchLookupData = Response<crate::routes::batch_lookup::BatchLookupData>;
pub type HealthData = Response<crate::routes::health::HealthData>;
pub type HistoryData = Response<crate::routes::key_history::HistoryData>;
pub type LookupData = Response<crate::routes::lookup::LookupData>;
pub type EpochData = Response<crate::routes::get_epoch_hash::EpochData>;
pub type PublicKeyData = Response<crate::routes::get_public_key::PublicKeyData>;
