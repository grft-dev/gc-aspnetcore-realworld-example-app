# Repository Guidance

## Overview

- This port exposes the RealWorld Conduit operations as a Graftcode module.
- `src/Conduit` is the class library Graftcode Gateway loads. Public methods are static, synchronous, and return `string` or `string[]`.
- `src/Conduit.GraftHost` references that library and contains no business code.
- `src/docker-compose.graft.yml` starts SQL Server, the idempotent seed, and the gateway.
- The end-to-end verifier is `gc-realworld-graft-e2e`, outside this repository. It is not part of `Conduit.slnx`.
- Runbook: [readme.md](readme.md).

## Toolchain

- Use the .NET SDK specified in `global.json` (`10.0.402`, with `latestFeature` roll-forward) and target `net10.0`.
- Package versions are managed centrally in `Directory.Packages.props`.
- Restore packages with lock files enabled. Restore updates to `packages.lock.json` files should be included when dependencies change.
- Nullable reference types, recommended analyzers, code-style checks, and warnings-as-errors are enabled in `Directory.Build.props`.
- CSharpier `1.3.0` is the repository formatter.

## Validation

Format and build:

```sh
dotnet run --project build/build.csproj -- test
```

The in-repo build is not the proof that the graft works. Start the stack from the readme and run `gc-realworld-graft-e2e`.
