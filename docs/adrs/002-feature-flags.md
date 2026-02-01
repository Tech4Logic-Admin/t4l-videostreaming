# ADR-002: Feature Flags for Local Development

## Status

Accepted

## Date

2026-01-31

## Context

The Tech4Logic Video Search application integrates with several Azure services:
- Azure Video Indexer (transcription, indexing)
- Azure AI Content Safety (moderation)
- Azure AI Search (semantic search)
- Microsoft Defender for Cloud (malware scanning)
- Azure Blob Storage (media storage)

During local development, we need a way to run the application without these services, either because:
1. Services are expensive or have quotas
2. Services require Azure subscriptions
3. Services are slow for rapid iteration
4. Offline development is needed

## Decision

We implement **feature flags** to toggle between real Azure services and mock implementations.

### Feature Flag Configuration

```json
{
  "FeatureFlags": {
    "UseDevAuth": true,
    "UseMockVideoIndexer": true,
    "UseMockContentSafety": true,
    "UseMockSearch": true,
    "UseMockMalwareScanner": true
  }
}
```

### Implementation Pattern

1. **Interface-based adapters**: Each external service has an interface
2. **Real implementation**: Uses actual Azure SDK
3. **Mock implementation**: Returns deterministic test data
4. **DI-based selection**: Feature flags control which implementation is injected

```csharp
if (featureFlags.UseMockVideoIndexer)
    services.AddSingleton<IVideoIndexerClient, MockVideoIndexerClient>();
else
    services.AddSingleton<IVideoIndexerClient, AzureVideoIndexerClient>();
```

## Rationale

1. **Zero Azure Cost for Development**: Developers can work without Azure subscriptions
2. **Fast Feedback Loops**: Mock implementations return immediately
3. **Deterministic Testing**: Mocks provide predictable responses
4. **Gradual Migration**: Can enable real services one at a time
5. **CI/CD Simplicity**: Tests run without external dependencies

## Consequences

### Positive
- Local development works offline
- Faster test execution
- Lower Azure costs during development
- Easy to test edge cases and error conditions

### Negative
- Mock implementations may drift from real service behavior
- Need to maintain two implementations
- Integration issues may only surface in staging/production

### Mitigations
- Regular integration tests against real services (nightly)
- Mock implementations match real API contracts
- Staging environment always uses real services
- Feature flags are never enabled in production

## Feature Flag States by Environment

| Environment | UseDevAuth | UseMock* | Real Services |
|-------------|------------|----------|---------------|
| Local       | ✅ true    | ✅ true  | ❌ No         |
| CI/Test     | ✅ true    | ✅ true  | ❌ No         |
| Staging     | ❌ false   | ❌ false | ✅ Yes        |
| Production  | ❌ false   | ❌ false | ✅ Yes        |

## References

- [Feature Flags - Martin Fowler](https://martinfowler.com/articles/feature-toggles.html)
- [Azure SDK Mocking Guidelines](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/samples/Mocking.md)
