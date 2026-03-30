use akd::{AkdLabel, AkdValue};
use bitwarden_encoding::B64;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::{BitwardenAkdLabelMaterial, BitwardenAkdPairMaterial};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdPairMaterialRequest {
    UserRealWorldId {
        real_world_id: String,
        user_id: Uuid,
    },
    UserPublicKey {
        user_id: Uuid,
        public_key_der_b64: B64,
    },
}

#[derive(Debug, thiserror::Error)]
pub enum RequestConversionError {
    #[error("Invalid base64 encoding: {0}")]
    InvalidBase64(#[from] bitwarden_encoding::NotB64EncodedError),
}

impl TryFrom<BitwardenAkdPairMaterialRequest> for BitwardenAkdPairMaterial {
    type Error = RequestConversionError;

    fn try_from(req: BitwardenAkdPairMaterialRequest) -> Result<Self, Self::Error> {
        Ok(match req {
            BitwardenAkdPairMaterialRequest::UserRealWorldId {
                real_world_id,
                user_id,
            } => BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id,
                user_id,
            },
            BitwardenAkdPairMaterialRequest::UserPublicKey {
                user_id,
                public_key_der_b64: public_key_der,
            } => BitwardenAkdPairMaterial::UserPublicKey {
                user_id,
                public_key_der: public_key_der.into_bytes(),
            },
        })
    }
}

impl TryFrom<BitwardenAkdPairMaterialRequest> for AkdLabel {
    type Error = RequestConversionError;

    fn try_from(req: BitwardenAkdPairMaterialRequest) -> Result<Self, Self::Error> {
        req.try_into().map(|l: BitwardenAkdPairMaterial| l.into())
    }
}

impl TryFrom<BitwardenAkdPairMaterialRequest> for AkdValue {
    type Error = RequestConversionError;

    fn try_from(req: BitwardenAkdPairMaterialRequest) -> Result<Self, Self::Error> {
        req.try_into().map(|l: BitwardenAkdPairMaterial| l.into())
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdLabelMaterialRequest {
    UserRealWorldId { real_world_id: String },
    UserPublicKey { user_id: Uuid },
}

impl TryFrom<BitwardenAkdLabelMaterialRequest> for BitwardenAkdLabelMaterial {
    type Error = RequestConversionError;

    fn try_from(req: BitwardenAkdLabelMaterialRequest) -> Result<Self, Self::Error> {
        Ok(match req {
            BitwardenAkdLabelMaterialRequest::UserRealWorldId { real_world_id } => {
                BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id }
            }
            BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id } => {
                BitwardenAkdLabelMaterial::UserPublicKey { user_id }
            }
        })
    }
}

impl TryFrom<BitwardenAkdLabelMaterialRequest> for AkdLabel {
    type Error = RequestConversionError;

    fn try_from(req: BitwardenAkdLabelMaterialRequest) -> Result<Self, Self::Error> {
        req.try_into().map(|l: BitwardenAkdLabelMaterial| l.into())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_pair_material_request_user_real_world_id_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com",
            "user_id": "550e8400-e29b-41d4-a716-446655440000"
        }"#;

        let req: BitwardenAkdPairMaterialRequest = serde_json::from_str(json).unwrap();
        let pair: BitwardenAkdPairMaterial = req.try_into().unwrap();

        let BitwardenAkdPairMaterial::UserRealWorldId {
            real_world_id,
            user_id,
        } = pair
        else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(real_world_id, "user@example.com");
        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").unwrap()
        );
    }

    #[test]
    fn test_pair_material_request_user_public_key_from_json() {
        let json = r#"{
            "type": "UserPublicKey",
            "user_id": "550e8400-e29b-41d4-a716-446655440000",
            "public_key_der_b64": "MIIBIQ=="
        }"#;

        let req: BitwardenAkdPairMaterialRequest = serde_json::from_str(json).unwrap();
        let pair: BitwardenAkdPairMaterial = req.try_into().unwrap();

        let BitwardenAkdPairMaterial::UserPublicKey {
            user_id,
            public_key_der,
        } = pair
        else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").unwrap()
        );
        assert_eq!(public_key_der, vec![48, 130, 1, 33]);
    }

    #[test]
    fn test_label_material_request_user_real_world_id_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com"
        }"#;

        let req: BitwardenAkdLabelMaterialRequest = serde_json::from_str(json).unwrap();
        let label = req.try_into();

        let Ok(BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id }) = label else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(real_world_id, "user@example.com");
    }

    #[test]
    fn test_label_material_request_user_public_key_from_json() {
        let json = r#"{
            "type": "UserPublicKey",
            "user_id": "550e8400-e29b-41d4-a716-446655440000"
        }"#;

        let req: BitwardenAkdLabelMaterialRequest = serde_json::from_str(json).unwrap();
        let label = req.try_into();

        let Ok(BitwardenAkdLabelMaterial::UserPublicKey { user_id }) = label else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").unwrap()
        );
    }
}
