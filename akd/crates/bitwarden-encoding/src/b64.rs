use std::str::FromStr;

use data_encoding::BASE64;
use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::FromStrVisitor;

/// Base64 encoded data
///
/// Is indifferent about padding when decoding, but always produces padding when encoding.
#[derive(Debug, Serialize, Clone, Hash, PartialEq, Eq)]
#[serde(into = "String")]
pub struct B64(Vec<u8>);

impl B64 {
    /// Returns a byte slice of the inner vector.
    pub fn as_bytes(&self) -> &[u8] {
        &self.0
    }

    /// Returns the inner byte vector.
    pub fn into_bytes(self) -> Vec<u8> {
        self.0
    }
}

// We manually implement this to handle both `String` and `&str`
impl<'de> Deserialize<'de> for B64 {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        deserializer.deserialize_str(FromStrVisitor::new())
    }
}

impl From<Vec<u8>> for B64 {
    fn from(src: Vec<u8>) -> Self {
        Self(src)
    }
}
impl From<&[u8]> for B64 {
    fn from(src: &[u8]) -> Self {
        Self(src.to_vec())
    }
}

impl From<B64> for Vec<u8> {
    fn from(src: B64) -> Self {
        src.0
    }
}

impl From<B64> for String {
    fn from(src: B64) -> Self {
        String::from(&src)
    }
}

impl From<&B64> for String {
    fn from(src: &B64) -> Self {
        BASE64.encode(&src.0)
    }
}

impl std::fmt::Display for B64 {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(String::from(self).as_str())
    }
}

/// An error returned when a string is not base64 decodable.
#[derive(Debug, Error)]
#[error("Data isn't base64 encoded")]
pub struct NotB64EncodedError;

const BASE64_PERMISSIVE: data_encoding::Encoding = data_encoding_macro::new_encoding! {
    symbols: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/",
    padding: None,
    check_trailing_bits: false,
};
const BASE64_PADDING: &str = "=";

impl TryFrom<String> for B64 {
    type Error = NotB64EncodedError;

    fn try_from(value: String) -> Result<Self, Self::Error> {
        Self::try_from(value.as_str())
    }
}

impl TryFrom<&str> for B64 {
    type Error = NotB64EncodedError;

    fn try_from(value: &str) -> Result<Self, Self::Error> {
        let sane_string = value.trim_end_matches(BASE64_PADDING);
        BASE64_PERMISSIVE
            .decode(sane_string.as_bytes())
            .map(Self)
            .map_err(|_| NotB64EncodedError)
    }
}

impl FromStr for B64 {
    type Err = NotB64EncodedError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        Self::try_from(s)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_b64_from_vec() {
        let data = vec![72, 101, 108, 108, 111];
        let b64 = B64::from(data.clone());
        assert_eq!(Vec::<u8>::from(b64), data);
    }

    #[test]
    fn test_b64_from_slice() {
        let data = b"Hello";
        let b64 = B64::from(data.as_slice());
        assert_eq!(b64.as_bytes(), data);
    }

    #[test]
    fn test_b64_encoding_with_padding() {
        let data = b"Hello, World!";
        let b64 = B64::from(data.as_slice());
        let encoded = String::from(&b64);
        assert_eq!(encoded, "SGVsbG8sIFdvcmxkIQ==");
        assert!(encoded.contains('='));
    }

    #[test]
    fn test_b64_decoding_with_padding() {
        let encoded_with_padding = "SGVsbG8sIFdvcmxkIQ==";
        let b64 = B64::try_from(encoded_with_padding).unwrap();
        assert_eq!(b64.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64_decoding_without_padding() {
        let encoded_without_padding = "SGVsbG8sIFdvcmxkIQ";
        let b64 = B64::try_from(encoded_without_padding).unwrap();
        assert_eq!(b64.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64_round_trip_with_padding() {
        let original = b"Test data that requires padding!";
        let b64 = B64::from(original.as_slice());
        let encoded = String::from(&b64);
        let decoded = B64::try_from(encoded.as_str()).unwrap();
        assert_eq!(decoded.as_bytes(), original);
    }

    #[test]
    fn test_b64_round_trip_without_padding() {
        let original = b"Test data";
        let b64 = B64::from(original.as_slice());
        let encoded = String::from(&b64);
        let decoded = B64::try_from(encoded.as_str()).unwrap();
        assert_eq!(decoded.as_bytes(), original);
    }

    #[test]
    fn test_b64_display() {
        let data = b"Hello";
        let b64 = B64::from(data.as_slice());
        assert_eq!(b64.to_string(), "SGVsbG8=");
    }

    #[test]
    fn test_b64_invalid_encoding() {
        let invalid_b64 = "This is not base64!@#$";
        let result = B64::try_from(invalid_b64);
        assert!(result.is_err());
    }

    #[test]
    fn test_b64_empty_string() {
        let empty = "";
        let b64 = B64::try_from(empty).unwrap();
        assert_eq!(b64.as_bytes().len(), 0);
    }

    #[test]
    fn test_b64_padding_removal() {
        let encoded_with_padding = "SGVsbG8sIFdvcmxkIQ==";
        let b64 = B64::try_from(encoded_with_padding).unwrap();
        assert_eq!(b64.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64_serialization() {
        let data = b"serialization test";
        let b64 = B64::from(data.as_slice());

        let serialized = serde_json::to_string(&b64).unwrap();
        assert_eq!(serialized, "\"c2VyaWFsaXphdGlvbiB0ZXN0\"");

        let deserialized: B64 = serde_json::from_str(&serialized).unwrap();
        assert_eq!(b64.as_bytes(), deserialized.as_bytes());
    }

    #[test]
    fn test_not_b64_encoded_error_display() {
        let error = NotB64EncodedError;
        assert_eq!(error.to_string(), "Data isn't base64 encoded");
    }

    #[test]
    fn test_b64_from_str() {
        let encoded = "SGVsbG8sIFdvcmxkIQ==";
        let b64: B64 = encoded.parse().unwrap();
        assert_eq!(b64.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64_eq_and_hash() {
        let data1 = b"test data";
        let data2 = b"test data";
        let data3 = b"different data";

        let b64_1 = B64::from(data1.as_slice());
        let b64_2 = B64::from(data2.as_slice());
        let b64_3 = B64::from(data3.as_slice());

        assert_eq!(b64_1, b64_2);
        assert_ne!(b64_1, b64_3);

        use std::{
            collections::hash_map::DefaultHasher,
            hash::{Hash, Hasher},
        };

        let mut hasher1 = DefaultHasher::new();
        let mut hasher2 = DefaultHasher::new();

        b64_1.hash(&mut hasher1);
        b64_2.hash(&mut hasher2);

        assert_eq!(hasher1.finish(), hasher2.finish());
    }
}
