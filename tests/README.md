# Tests

This directory hosts the automated test suites for Ten Second Tom.

## Target layout

```
tests/
├── TenSecondTom.Tests/                  # Unit tests (fast)
│   ├── Architecture/
│   ├── Features/<FeatureName>/<UseCase>Tests.cs
│   ├── Infrastructure/<Area>Tests.cs
│   ├── Models/<Type>Tests.cs
│   └── Shared/<Utility>Tests.cs
└── TenSecondTom.IntegrationTests/       # Integration / CLI / workflow tests
    ├── DisableParallelization.cs
    ├── GlobalSuppressions.cs
    ├── Integration/
    │   ├── Cli/<Command>Tests.cs
    │   ├── Features/<FeatureName>/<Scenario>Tests.cs
    │   ├── Infrastructure/<Concern>Tests.cs
    │   ├── Shared/<Component>Tests.cs
    │   └── Workflows/<Pipeline>Tests.cs
    └── TestHelpers/
```

The structure mirrors the Vertical Slice Architecture: every feature owns its tests, and supporting infrastructure/tests live alongside the area they exercise.

## Project directories

### `tests/TenSecondTom.Tests`
- `Architecture/` — automated VSA compliance checks.
- `Features/<FeatureName>/` — slice-specific unit tests grouped by handlers, services, etc.
- `Infrastructure/<Area>/`, `Models/`, `Shared/` — cross-cutting unit tests that support multiple features.
- `TestHelpers/` — reusable fixtures (e.g., audio samples).

### `tests/TenSecondTom.IntegrationTests`
- `Integration/Cli/` — end-to-end command coverage for the public CLI surface.
- `Integration/Features/<FeatureName>/` — multi-component feature flows.
- `Integration/Infrastructure`, `Integration/Shared`, `Integration/Workflows` — scenarios that span multiple features or deployment workflows.
- `TestHelpers/` — integration-specific fixtures and user-secret utilities.

## Feature coverage map

| Feature | Unit tests | Integration tests |
| --- | --- | --- |
| Audio | `tests/TenSecondTom.Tests/Features/Audio` | `tests/TenSecondTom.IntegrationTests/Integration/Features/Audio` |
| Auth | `tests/TenSecondTom.Tests/Features/Auth` | `tests/TenSecondTom.IntegrationTests/Integration/Cli/AuthCommandTests.cs` |
| Generate | `tests/TenSecondTom.Tests/Features/Generate` | `tests/TenSecondTom.IntegrationTests/Integration/Features/Generate` |
| Search | `tests/TenSecondTom.Tests/Features/Search` | — |
| Setup | `tests/TenSecondTom.Tests/Features/Setup` | `tests/TenSecondTom.IntegrationTests/Integration/Features/Setup` |
| Shell | `tests/TenSecondTom.Tests/Features/Shell` | `tests/TenSecondTom.IntegrationTests/Integration/Cli` |
| Templates | `tests/TenSecondTom.Tests/Features/Templates` | `tests/TenSecondTom.IntegrationTests/Integration/Features/Templates` |
| ThisWeek | `tests/TenSecondTom.Tests/Features/ThisWeek` | `tests/TenSecondTom.IntegrationTests/Integration/Features/ThisWeek` |
| Today | `tests/TenSecondTom.Tests/Features/Today` | `tests/TenSecondTom.IntegrationTests/Integration/Features/Today` |

> Use the table as a quick inventory to identify features lacking integration coverage (currently `Search`) or additional CLI coverage needs.

## Follow-up gaps

- [ ] Add integration coverage for `tests/TenSecondTom.Tests/Features/Search` (no matching scenarios under `tests/TenSecondTom.IntegrationTests/Integration/Features/Search` yet).
