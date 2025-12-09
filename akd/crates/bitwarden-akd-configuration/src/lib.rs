// Copyright (c) Meta Platforms, Inc. and affiliates.

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// MODIFICATIONS FROM ORIGINAL
//
// 1. Updates have been made to the original code published at
// https://github.com/facebook/akd/blob/04d1988292f2c5eddc820722dcd36c0eda8d5bc3/akd_core/src/configuration/whatsapp_v1.rs
// https://github.com/facebook/akd/blob/04d1988292f2c5eddc820722dcd36c0eda8d5bc3/akd_core/src/configuration/experimental.rs
// For the Bitwarden use-case. Generally, changes for the experimental configuration were taken, except for the weaker
// context binding for compute_parent_hash_from_children
// 2. INSTALLATION_CONTEXT has been added to provide Bitwarden self-host / cloud installation separation

//! Define the Bitwarden V1 configuration

#[cfg(feature = "config")]
pub mod config;

use akd::configuration::Configuration;
use akd::hash::{Digest, DIGEST_BYTES};
use akd::{AkdLabel, AkdValue, AzksValue, AzksValueWithEpoch, NodeLabel, VersionFreshness};
use std::sync::OnceLock;
use uuid::Uuid;

/// Bitwarden installation ID for instance separation
static INSTALLATION_CONTEXT: OnceLock<Vec<u8>> = OnceLock::new();
const BITWARDEN_V1: &[u8] = b"BWv1";

#[derive(Clone)]
pub struct BitwardenV1Configuration;

impl BitwardenV1Configuration {
    /// Initialize the global installation context. Must be called once before any use.
    ///
    /// # Panics
    /// Panics if called more than once.
    pub fn init(installation_context: Uuid) {
        let installation_context = [BITWARDEN_V1, installation_context.as_bytes()].concat();
        INSTALLATION_CONTEXT
            .set(installation_context)
            .expect("BitwardenV1Configuration already initialized");
    }

    /// Get the installation context. Panics if not initialized.
    /// # Panics
    /// Panics if `BitwardenV1Configuration::init()` has not been called.
    fn get_context() -> &'static [u8] {
        INSTALLATION_CONTEXT
            .get()
            .expect("BitwardenV1Configuration::init() must be called before use")
    }

    /// Used by the client to supply a commitment nonce and value to reconstruct the commitment, via:
    /// commitment = H(i2osp_array(value), i2osp_array(nonce))
    fn generate_commitment_from_nonce_client(value: &crate::AkdValue, nonce: &[u8]) -> AzksValue {
        AzksValue(<Self as Configuration>::hash(
            &[i2osp_array(value), i2osp_array(nonce)].concat(),
        ))
    }
}

/// Corresponds to the I2OSP() function from RFC8017, prepending the length of
/// a byte array to the byte array (so that it is ready for serialization and hashing)
///
/// Input byte array cannot be > 2^64-1 in length
pub fn i2osp_array(input: &[u8]) -> Vec<u8> {
    [&(input.len() as u64).to_be_bytes(), input].concat()
}

impl Configuration for BitwardenV1Configuration {
    fn hash(item: &[u8]) -> akd::hash::Digest {
        // Hash(installation_context || item)
        let mut hasher = blake3::Hasher::new();
        hasher.update(Self::get_context());
        hasher.update(item);
        hasher.finalize().into()
    }

    fn empty_root_value() -> AzksValue {
        AzksValue([0u8; 32])
    }

    fn empty_node_hash() -> AzksValue {
        AzksValue([0u8; 32])
    }

    fn hash_leaf_with_value(value: &akd::AkdValue, epoch: u64, nonce: &[u8]) -> AzksValueWithEpoch {
        let commitment = Self::generate_commitment_from_nonce_client(value, nonce);
        Self::hash_leaf_with_commitment(commitment, epoch)
    }

    fn hash_leaf_with_commitment(commitment: AzksValue, epoch: u64) -> AzksValueWithEpoch {
        let mut data = [0; DIGEST_BYTES + 8];
        data[..DIGEST_BYTES].copy_from_slice(&commitment.0);
        data[DIGEST_BYTES..].copy_from_slice(&epoch.to_be_bytes());
        AzksValueWithEpoch(Self::hash(&data))
    }

    /// Used by the server to produce a commitment nonce for an AkdLabel, version, and AkdValue.
    /// Computes nonce = H(commitment key || label || version || value)
    fn get_commitment_nonce(
        commitment_key: &[u8],
        label: &NodeLabel,
        version: u64,
        value: &AkdValue,
    ) -> Digest {
        Self::hash(
            &[
                commitment_key,
                &[&label.label_len.to_be_bytes(), &label.label_val[..]].concat(),
                &version.to_be_bytes(),
                &i2osp_array(value),
            ]
            .concat(),
        )
    }

    /// Used by the server to produce a commitment for an AkdLabel, version, and AkdValue
    ///
    /// nonce = H(commitment_key, label, version, i2osp_array(value))
    /// commmitment = H(i2osp_array(value), i2osp_array(nonce))
    ///
    /// The nonce value is used to create a hiding and binding commitment using a
    /// cryptographic hash function. Note that it is derived from the label, version, and
    /// value (even though the binding to value is somewhat optional).
    ///
    /// Note that this commitment needs to be a hash function (random oracle) output
    fn compute_fresh_azks_value(
        commitment_key: &[u8],
        label: &NodeLabel,
        version: u64,
        value: &AkdValue,
    ) -> AzksValue {
        let nonce = Self::get_commitment_nonce(commitment_key, label, version, value);
        AzksValue(Self::hash(
            &[i2osp_array(value), i2osp_array(&nonce)].concat(),
        ))
    }

    /// To convert a regular label (arbitrary string of bytes) into a [NodeLabel], we compute the
    /// output as: H(label || freshness || version)
    ///
    /// Specifically, we concatenate the following together:
    /// - I2OSP(len(label) as u64, label)
    /// - A single byte encoded as 0u8 if "stale", 1u8 if "fresh"
    /// - A u64 representing the version
    ///
    /// These are all interpreted as a single byte array and hashed together, with the output
    /// of the hash returned.
    fn get_hash_from_label_input(
        label: &AkdLabel,
        freshness: VersionFreshness,
        version: u64,
    ) -> Vec<u8> {
        let freshness_bytes = [freshness as u8];
        let hashed_label = Self::hash(
            &[
                &i2osp_array(label)[..],
                &freshness_bytes,
                &version.to_be_bytes(),
            ]
            .concat(),
        );
        hashed_label.to_vec()
    }

    /// Computes the parent hash from the children hashes and labels
    fn compute_parent_hash_from_children(
        left_val: &AzksValue,
        left_label: &[u8],
        right_val: &AzksValue,
        right_label: &[u8],
    ) -> AzksValue {
        AzksValue(Self::hash(
            &[&left_val.0, left_label, &right_val.0, right_label].concat(),
        ))
    }

    /// Given the top-level hash, compute the "actual" root hash that is published
    /// by the directory maintainer
    fn compute_root_hash_from_val(root_val: &AzksValue) -> Digest {
        root_val.0
    }

    /// Similar to commit_fresh_value, but used for stale values.
    fn stale_azks_value() -> AzksValue {
        AzksValue(akd::hash::EMPTY_DIGEST)
    }

    fn compute_node_label_value(bytes: &[u8]) -> Vec<u8> {
        bytes.to_vec()
    }

    fn empty_label() -> NodeLabel {
        NodeLabel {
            label_val: [
                1u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8,
                0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8, 0u8,
            ],
            label_len: 0,
        }
    }
}

#[cfg(test)]
mod tests {
    use crate::BitwardenV1Configuration;

    trait EnsureSendSync: Send + Sync {}
    impl<T: Send + Sync> EnsureSendSync for T {}

    #[test]
    fn test_bitwarden_v1_configuration_is_send_sync() {
        let _assert: &dyn EnsureSendSync = &BitwardenV1Configuration;
    }
}
