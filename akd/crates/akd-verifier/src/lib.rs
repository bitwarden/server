//! Client-side verifier for the Bitwarden AKD (Auditable Key Directory).
//!
//! [`AkdVerifier`] talks to two services:
//! - the **AKD server**, which serves AKD lookup proofs and key histories,
//! - the **auditor** (a server running **akd-watch** targeting the AKD server), which signs each epoch's
//!   root hash so clients can confirm the directory has been audited (blocks split-worldview attacks).
//!
//! ## Operations
//!
//! - [`lookup`] / [`lookup_batch`] — fetch the current value(s) for one or more labels.
//! - [`verify_pair`] / [`verify_pairs`] — confirm a caller-supplied `(label, value)` pair
//!   matches what the AKD server has committed.
//! - [`lookup_history`] — fetch the full version history for a label.
//! - [`audited`] — poll the auditor for whether a specific epoch has been signed.
//!   Useful after an `EpochNotAudited` outcome.
//!
//! Every operation except [`audited`] fetches a proof from the AKD server, verifies
//! it, and confirms the proof's root has been signed by the auditor — only then is
//! a [`VerifiedValue`] returned.
//!
//! ```no_run
//! # use akd_verifier::{AkdVerifier, BitwardenAkdLabelMaterialRequest};
//! # async fn run(label: BitwardenAkdLabelMaterialRequest) -> Result<(), Box<dyn std::error::Error>> {
//! let verifier = AkdVerifier::new(
//!     "https://akd.example".into(),
//!     "https://auditor.example".into(),
//!     "production".into(),
//!     uuid::Uuid::nil(),
//! )?;
//! let value = verifier.lookup(label).await?;
//! println!("epoch={} version={}", value.epoch, value.version);
//! # Ok(()) }
//! ```
//!
//! [`lookup`]: AkdVerifier::lookup
//! [`lookup_batch`]: AkdVerifier::lookup_batch
//! [`verify_pair`]: AkdVerifier::verify_pair
//! [`verify_pairs`]: AkdVerifier::verify_pairs
//! [`lookup_history`]: AkdVerifier::lookup_history
//! [`audited`]: AkdVerifier::audited

pub mod error;
pub mod models;
pub mod verifier;
mod verify;

#[cfg(feature = "uniffi")]
uniffi::setup_scaffolding!("akd_verifier");

pub use error::{
    BatchLabelError, BatchLookupError, BatchPairError, BatchVerifyError, FailedLookupItem,
    FailedVerifyItem, InvalidUrl, LookupError, TransportError, VerifiedItem, VerifyError,
    VerifyItemError,
};
pub use models::{
    BitwardenAkdLabelMaterialRequest, BitwardenAkdPairMaterialRequest, VerifiedValue,
};
pub use verifier::AkdVerifier;
