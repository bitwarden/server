default:
  @just --list

all: lint test build upload

lint:
  dotnet format --verify-no-changes

test:
  echo "Testing..."

build:
  # Login to ACR
  az acr login -n bitwardenprod.azurecr.io
  if `grep "pull"` <<< "${GITHUB_REF}"; then
  IMAGE_TAG := `echo "${GITHUB_HEAD_REF}" | sed "s#/#-#g"`
  else
  IMAGE_TAG := `echo "${GITHUB_REF:11}" | sed "s#/#-#g"`
  fi

  if "${IMAGE_TAG}" == "main"; then
  IMAGE_TAG := dev
  fi

  echo ${PROJECT_NAME}
  PROJECT_NAME := `echo "$PROJECT_NAME" | awk '{print tolower($0)}'`
  echo ${PROJECT_NAME}

upload:
  echo "Uploading..."
