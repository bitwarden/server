use akd::{AkdLabel, AkdValue};
use bitwarden_encoding::B64;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::{BitwardenAkdLabelMaterial, BitwardenAkdPairMaterial};

#[cfg_attr(feature = "uniffi", derive(uniffi::Enum))]
#[cfg_attr(
    feature = "wasm",
    derive(tsify::Tsify),
    tsify(into_wasm_abi, from_wasm_abi)
)]
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdPairMaterialRequest {
    UserRealWorldId {
        real_world_id: String,
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        user_id: Uuid,
    },
    UserPublicKey {
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        user_id: Uuid,
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        public_key_der_b64: B64,
    },
}

impl From<BitwardenAkdPairMaterialRequest> for BitwardenAkdPairMaterial {
    fn from(req: BitwardenAkdPairMaterialRequest) -> Self {
        match req {
            BitwardenAkdPairMaterialRequest::UserRealWorldId {
                real_world_id,
                user_id,
            } => BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id,
                user_id,
            },
            BitwardenAkdPairMaterialRequest::UserPublicKey {
                user_id,
                public_key_der_b64,
            } => BitwardenAkdPairMaterial::UserPublicKey {
                user_id,
                public_key_der: public_key_der_b64.into_bytes(),
            },
        }
    }
}

impl From<BitwardenAkdPairMaterialRequest> for AkdLabel {
    fn from(req: BitwardenAkdPairMaterialRequest) -> Self {
        BitwardenAkdPairMaterial::from(req).into()
    }
}

impl From<BitwardenAkdPairMaterialRequest> for AkdValue {
    fn from(req: BitwardenAkdPairMaterialRequest) -> Self {
        BitwardenAkdPairMaterial::from(req).into()
    }
}

#[cfg_attr(feature = "uniffi", derive(uniffi::Enum))]
#[cfg_attr(
    feature = "wasm",
    derive(tsify::Tsify),
    tsify(into_wasm_abi, from_wasm_abi)
)]
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdLabelMaterialRequest {
    UserRealWorldId {
        real_world_id: String,
    },
    UserPublicKey {
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        user_id: Uuid,
    },
}

impl From<BitwardenAkdLabelMaterialRequest> for BitwardenAkdLabelMaterial {
    fn from(value: BitwardenAkdLabelMaterialRequest) -> Self {
        match value {
            BitwardenAkdLabelMaterialRequest::UserRealWorldId { real_world_id } => {
                BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id }
            }
            BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id } => {
                BitwardenAkdLabelMaterial::UserPublicKey { user_id }
            }
        }
    }
}

impl From<BitwardenAkdLabelMaterialRequest> for AkdLabel {
    fn from(value: BitwardenAkdLabelMaterialRequest) -> Self {
        BitwardenAkdLabelMaterial::from(value).into()
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

        let req: BitwardenAkdPairMaterialRequest = serde_json::from_str(json).expect("valid json");
        let pair: BitwardenAkdPairMaterial = req.into();

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
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").expect("valid uuid")
        );
    }

    #[test]
    fn test_pair_material_request_user_public_key_from_json() {
        let json = r#"{
            "type": "UserPublicKey",
            "user_id": "550e8400-e29b-41d4-a716-446655440000",
            "public_key_der_b64": "MIIBIQ=="
        }"#;

        let req: BitwardenAkdPairMaterialRequest = serde_json::from_str(json).expect("valid json");
        let pair: BitwardenAkdPairMaterial = req.into();

        let BitwardenAkdPairMaterial::UserPublicKey {
            user_id,
            public_key_der,
        } = pair
        else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").expect("valid uuid")
        );
        assert_eq!(public_key_der, vec![48, 130, 1, 33]);
    }

    #[test]
    fn test_label_material_request_user_real_world_id_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com"
        }"#;

        let req: BitwardenAkdLabelMaterialRequest = serde_json::from_str(json).expect("valid json");
        let label: BitwardenAkdLabelMaterial = req.into();

        let BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id } = label else {
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

        let req: BitwardenAkdLabelMaterialRequest = serde_json::from_str(json).expect("valid json");
        let label: BitwardenAkdLabelMaterial = req.into();

        let BitwardenAkdLabelMaterial::UserPublicKey { user_id } = label else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").expect("valid uuid")
        );
    }
}
