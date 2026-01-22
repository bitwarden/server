use akd::errors::AkdError;
use axum::http::StatusCode;
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Main error type for the Reader API
///
/// Note: Base64 validation for input fields happens automatically during JSON
/// deserialization via the `bitwarden_encoding::B64` type, so invalid base64
/// will be rejected before reaching the handlers with a 400 Bad Request error.
#[derive(Error, Debug)]
pub enum ReaderError {
    // Application-level validation errors (4xx)
    #[error("Invalid epoch range: start_epoch ({start_epoch}) must be <= end_epoch ({end_epoch})")]
    InvalidEpochRange { start_epoch: u64, end_epoch: u64 },

    #[error("Empty batch request")]
    EmptyBatch,

    #[error("Batch size limit exceeded: {limit}")]
    BatchTooLarge { limit: usize },

    // AKD library errors
    #[error("AKD error: {0}")]
    Akd(#[from] AkdError),
}

/// Error response structure sent to clients
#[derive(Debug, Serialize, Deserialize)]
pub struct ErrorResponse {
    /// Machine-readable error code for client-side matching
    pub code: ErrorCode,
    /// Human-readable error message
    pub message: String,
}

/// Machine-readable error codes for reliable client-side parsing
#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum ErrorCode {
    // Application-level validation errors (400-level)
    InvalidEpochRange,
    EmptyBatch,
    BatchTooLarge,

    // AKD-specific errors
    AkdTreeNode,
    AkdVerification,
    AkdInvalidEpoch,
    AkdInvalidVersion,
    AkdReadOnlyDirectory,
    AkdPublish,
    AkdAzks,
    AkdVrf,
    AkdStorageNotFound,
    AkdStorageTransaction,
    AkdStorageConnection,
    AkdStorageOther,
    AkdAudit,
    AkdParallelism,

    // Server errors (5xx)
    InternalError,
}

impl ReaderError {
    /// Map error to appropriate HTTP status code
    pub fn status_code(&self) -> StatusCode {
        match self {
            // 400-level errors
            ReaderError::InvalidEpochRange { .. } => StatusCode::BAD_REQUEST,
            ReaderError::EmptyBatch => StatusCode::BAD_REQUEST,
            ReaderError::BatchTooLarge { .. } => StatusCode::BAD_REQUEST,

            // AKD errors - nuanced mapping
            ReaderError::Akd(akd_err) => match akd_err {
                AkdError::Storage(storage_err) => match storage_err {
                    akd::errors::StorageError::NotFound(_) => StatusCode::NOT_FOUND,
                    akd::errors::StorageError::Transaction(_) => StatusCode::INTERNAL_SERVER_ERROR,
                    akd::errors::StorageError::Connection(_) => StatusCode::SERVICE_UNAVAILABLE,
                    akd::errors::StorageError::Other(_) => StatusCode::INTERNAL_SERVER_ERROR,
                },
                AkdError::Directory(dir_err) => match dir_err {
                    akd::errors::DirectoryError::InvalidEpoch(_) => StatusCode::BAD_REQUEST,
                    akd::errors::DirectoryError::InvalidVersion(_) => StatusCode::BAD_REQUEST,
                    akd::errors::DirectoryError::Verification(_) => {
                        StatusCode::UNPROCESSABLE_ENTITY
                    }
                    akd::errors::DirectoryError::ReadOnlyDirectory(_) => StatusCode::FORBIDDEN,
                    akd::errors::DirectoryError::Publish(_) => StatusCode::INTERNAL_SERVER_ERROR,
                },
                AkdError::TreeNode(_) => StatusCode::INTERNAL_SERVER_ERROR,
                AkdError::AzksErr(_) => StatusCode::INTERNAL_SERVER_ERROR,
                AkdError::Vrf(_) => StatusCode::INTERNAL_SERVER_ERROR,
                AkdError::AuditErr(_) => StatusCode::INTERNAL_SERVER_ERROR,
                AkdError::Parallelism(_) => StatusCode::INTERNAL_SERVER_ERROR,
                AkdError::TestErr(_) => StatusCode::INTERNAL_SERVER_ERROR,
            },
        }
    }

    /// Convert error to error code for client parsing
    pub fn error_code(&self) -> ErrorCode {
        match self {
            ReaderError::InvalidEpochRange { .. } => ErrorCode::InvalidEpochRange,
            ReaderError::EmptyBatch => ErrorCode::EmptyBatch,
            ReaderError::BatchTooLarge { .. } => ErrorCode::BatchTooLarge,
            ReaderError::Akd(akd_err) => Self::akd_error_code(akd_err),
        }
    }

    /// Map AkdError to specific error code
    fn akd_error_code(err: &AkdError) -> ErrorCode {
        match err {
            AkdError::TreeNode(_) => ErrorCode::AkdTreeNode,
            AkdError::Directory(dir_err) => match dir_err {
                akd::errors::DirectoryError::Verification(_) => ErrorCode::AkdVerification,
                akd::errors::DirectoryError::InvalidEpoch(_) => ErrorCode::AkdInvalidEpoch,
                akd::errors::DirectoryError::InvalidVersion(_) => ErrorCode::AkdInvalidVersion,
                akd::errors::DirectoryError::ReadOnlyDirectory(_) => {
                    ErrorCode::AkdReadOnlyDirectory
                }
                akd::errors::DirectoryError::Publish(_) => ErrorCode::AkdPublish,
            },
            AkdError::AzksErr(_) => ErrorCode::AkdAzks,
            AkdError::Vrf(_) => ErrorCode::AkdVrf,
            AkdError::Storage(storage_err) => match storage_err {
                akd::errors::StorageError::NotFound(_) => ErrorCode::AkdStorageNotFound,
                akd::errors::StorageError::Transaction(_) => ErrorCode::AkdStorageTransaction,
                akd::errors::StorageError::Connection(_) => ErrorCode::AkdStorageConnection,
                akd::errors::StorageError::Other(_) => ErrorCode::AkdStorageOther,
            },
            AkdError::AuditErr(_) => ErrorCode::AkdAudit,
            AkdError::Parallelism(_) => ErrorCode::AkdParallelism,
            AkdError::TestErr(_) => ErrorCode::InternalError,
        }
    }

    /// Convert error to ErrorResponse
    pub fn to_error_response(&self) -> ErrorResponse {
        ErrorResponse {
            code: self.error_code(),
            message: self.to_string(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_akd_not_found_maps_to_404() {
        let storage_err = akd::errors::StorageError::NotFound("test".to_string());
        let akd_err = AkdError::Storage(storage_err);
        let reader_err = ReaderError::from(akd_err);

        assert_eq!(reader_err.status_code(), StatusCode::NOT_FOUND);
        assert!(matches!(
            reader_err.error_code(),
            ErrorCode::AkdStorageNotFound
        ));
    }

    #[test]
    fn test_empty_batch_maps_to_400() {
        let err = ReaderError::EmptyBatch;
        assert_eq!(err.status_code(), StatusCode::BAD_REQUEST);
        assert!(matches!(err.error_code(), ErrorCode::EmptyBatch));
    }

    #[test]
    fn test_batch_too_large_maps_to_400() {
        let err = ReaderError::BatchTooLarge { limit: 1000 };
        assert_eq!(err.status_code(), StatusCode::BAD_REQUEST);
        assert!(matches!(err.error_code(), ErrorCode::BatchTooLarge));
    }

    #[test]
    fn test_invalid_epoch_range_maps_to_400() {
        let err = ReaderError::InvalidEpochRange {
            start_epoch: 0,
            end_epoch: 0,
        };
        assert_eq!(err.status_code(), StatusCode::BAD_REQUEST);
        assert!(matches!(err.error_code(), ErrorCode::InvalidEpochRange));
    }

    #[test]
    fn test_error_response_serialization() {
        let err = ReaderError::InvalidEpochRange {
            start_epoch: 0,
            end_epoch: 0,
        };
        let response = err.to_error_response();

        assert!(matches!(response.code, ErrorCode::InvalidEpochRange));
        eprintln!("Error message: {}", response.message);
        assert!(response.message.contains("Invalid epoch range"));
    }

    #[test]
    fn test_akd_invalid_epoch_error_maps_to_400() {
        let dir_err = akd::errors::DirectoryError::InvalidEpoch("invalid epoch".to_string());
        let akd_err = AkdError::Directory(dir_err);
        let reader_err = ReaderError::from(akd_err);

        assert_eq!(reader_err.status_code(), StatusCode::BAD_REQUEST);
        assert!(matches!(
            reader_err.error_code(),
            ErrorCode::AkdInvalidEpoch
        ));
    }

    #[test]
    fn test_akd_connection_error_maps_to_503() {
        let storage_err = akd::errors::StorageError::Connection("connection failed".to_string());
        let akd_err = AkdError::Storage(storage_err);
        let reader_err = ReaderError::from(akd_err);

        assert_eq!(reader_err.status_code(), StatusCode::SERVICE_UNAVAILABLE);
        assert!(matches!(
            reader_err.error_code(),
            ErrorCode::AkdStorageConnection
        ));
    }
}
