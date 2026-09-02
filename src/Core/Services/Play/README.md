# Play Services

## Overview

The Play services provide automated testing infrastructure for tracking and cleaning up test data in development
environments. A "Play" is a test session that groups entities (users, organizations, etc.) created during testing to
enable bulk cleanup via the SeederAPI.

## How It Works

1. Test client sends `x-play-id` header with a unique Play identifier
2. `PlayIdMiddleware` extracts the header and sets it on `IPlayIdService`
3. Repositories check `IPlayIdService.InPlay()` when creating entities
4. `IPlayItemService` records PlayItem entries for tracked entities
5. SeederAPI uses PlayItem records to bulk delete all entities associated with a PlayId

Play services are **only active in Development environments**.

## Classes

- **`IPlayIdService`** - Interface for managing Play identifiers in the current request scope
- **`IPlayItemService`** - Interface for tracking entities created during a Play session
- **`PlayIdService`** - Default scoped implementation for tracking Play sessions per HTTP request
- **`NeverPlayIdServices`** - No-op implementation used as fallback when no HttpContext is available
- **`PlayIdSingletonService`** - Singleton wrapper that allows singleton services to access scoped PlayIdService via
  HttpContext
- **`PlayItemService`** - Implementation that records PlayItem entries for entities created during Play sessions
