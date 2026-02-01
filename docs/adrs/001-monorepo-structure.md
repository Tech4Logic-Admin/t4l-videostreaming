# ADR-001: Monorepo Structure with Polyglot Services

## Status

Accepted

## Date

2026-01-31

## Context

We need to structure the Tech4Logic Video Search application which consists of multiple services:
- A Next.js frontend (TypeScript/Node.js)
- An ASP.NET Core API (.NET 8)
- Azure Functions workers (.NET 8)
- Shared infrastructure (Docker, CI/CD)

We considered several options:
1. **Monorepo**: All code in a single repository
2. **Multi-repo**: Separate repositories for each service
3. **Polyrepo with shared packages**: Multiple repos with published shared packages

## Decision

We chose a **monorepo structure** using pnpm workspaces for the Node.js portion and a flat `apps/` directory for all services.

```
/
├── apps/
│   ├── web/       # Next.js frontend
│   ├── api/       # ASP.NET Core API
│   └── worker/    # Azure Functions
├── packages/      # Shared TypeScript packages (future)
├── scripts/       # Build and deployment scripts
├── docs/          # Documentation
└── infra/         # Infrastructure as Code (future)
```

## Rationale

1. **Atomic Changes**: Feature development often spans multiple services. A monorepo allows atomic commits and PRs that modify frontend, backend, and infrastructure together.

2. **Simplified CI/CD**: One repository means one CI pipeline that can build, test, and deploy all services with coordinated versioning.

3. **Shared Tooling**: Common linting, formatting, and commit hooks across all services.

4. **Developer Experience**: Engineers can clone one repo and have the entire system ready to develop.

5. **Docker Compose Integration**: All services in one repo makes local development with docker-compose seamless.

## Consequences

### Positive
- Single source of truth for all code
- Easier refactoring across service boundaries
- Coordinated releases
- Simplified onboarding

### Negative
- Larger repository size over time
- Need for good code organization discipline
- CI may run longer (mitigated with selective builds)
- Different teams may need different tool chains (.NET vs Node.js)

### Mitigations
- Use workspace-aware tools (pnpm, Turborepo potential future)
- Implement CI caching and selective builds
- Clear directory boundaries and ownership

## References

- [Monorepos: Please don't!](https://medium.com/@mattklein123/monorepos-please-dont-e9a279be011b) - Counter-arguments considered
- [pnpm Workspaces](https://pnpm.io/workspaces)
- [Microsoft Monorepo Examples](https://github.com/microsoft/rushstack)
