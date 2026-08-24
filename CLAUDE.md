# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

KnightBus is a .NET messaging framework whose defining trait is that the **transport is a property of
the message type**, not of the application: one host can listen to Azure Service Bus, PostgreSQL and
Redis simultaneously, and a message is routed by the marker interface it implements.

For how to *use* the library — every marker interface, transport capability and behavioural gotcha —
read `docs/` (published at <https://bookbeat.github.io/knightbus/>). Start with
`docs/reference/marker-interfaces.md`. This file covers how to *work in the repository*; don't
duplicate library documentation here, or the two will drift.

## Repository layout

This is a **merged multi-repo, and there is no top-level `src/`**. Expect to search wider than usual:

```
knightbus/src/            Core, Host, Messages, Core.Management  (the framework)
knightbus/tests/          Framework tests
knightbus/examples/       8 runnable sample apps, built by CI
knightbus-<name>/src/     One folder per transport or integration
knightbus-<name>/tests/   Its tests
docs/                     Documentation site, and the brand assets
```

Every publishable project lives at `<area>/src/<Package.Name>/`. Anything outside `*/src/*` is not a
package.

## Commands

The solution is `KnightBus.slnx` (new-style solution format). `global.json` pins the SDK to .NET 10;
packages multi-target `net9.0;net10.0`.

```bash
dotnet build KnightBus.slnx
dotnet test                                    # whole solution
dotnet test knightbus/tests/KnightBus.Core.Tests.Unit    # one project
dotnet test --filter "FullyQualifiedName~SagaMiddleware"  # one test or fixture (NUnit)
```

Integration tests need backing services. CI starts these two; do the same locally, or expect those
projects to fail:

```bash
docker run -d -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
docker run -d -p 4222:4222 nats:latest
```

Test projects are split `*.Tests.Unit` and `*.Tests.Integration`, so filtering by project name is the
practical way to skip the ones needing infrastructure. The PostgreSQL, SQL Server and Redis
integration tests need their own servers, which CI does not start.

### Formatting is a CI gate

`dotnet csharpier check .` runs in CI and **fails the build** on unformatted code. CSharpier formats
`.csproj` XML as well as C#, so run it after editing project files too:

```bash
dotnet tool restore
dotnet csharpier format .
```

There is a `pre-commit` script at the repo root that does this; it is not installed automatically:

```bash
cp pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

Note the hook exits early unless staged changes include a `.cs` file, so a **`.csproj`-only change
slips past it** and fails CI instead. Run the formatter by hand after editing project files.

### Documentation

```bash
pip install --require-hashes --only-binary :all: -r docs/requirements.txt
mkdocs serve                  # live preview on :8000
mkdocs build --strict         # what CI runs
```

`--strict` turns a broken internal link or a page missing from `mkdocs.yml`'s `nav` into a build
failure. `docs/requirements.txt` is **hash-locked**: to upgrade, bump `mkdocs-material`, reinstall
into a clean venv, `pip freeze`, then regenerate a sha256 for every distribution PyPI publishes for
each pinned version. A version bump without new hashes fails the install rather than upgrading.

## Packaging and release

Shared package metadata lives in the root **`Directory.Build.props`** — authors, licence, project
URL, icon. Per-project values stay in each `.csproj`, and two of them must not be centralized:

- **`<Version>`** — `.github/workflows/pre-release.yaml` decides what to publish by diffing
  `<Version>` elements in `.csproj` files against `master`. Moving versions into shared props breaks
  that detection.
- **`<GeneratePackageOnBuild>`** — must not reach test or example projects.

`docs/assets/images/knighbus-64.png` (the misspelling is historical) is the `PackageIcon` for all 26
packages. Deleting or renaming it fails `dotnet pack` with `NU5046` everywhere.

**New non-shipping projects need `IsPackable=false`.** CI runs `dotnet pack` across the whole
solution, so anything packable is published. Projects under `knightbus/examples/` inherit it from
`knightbus/examples/Directory.Build.props`; a test project referencing `Microsoft.NET.Test.Sdk` gets it
for free; anything else — a test *helper* library, for instance — must set it explicitly or it ships
to nuget.org.

To release a package: bump its `<Version>`, add a `CHANGELOG.md` entry, merge to `master`. CI packs
and pushes with `--skip-duplicate`, so only genuinely-changed versions publish. The **Publish
Pre-Release Packages (Manual)** workflow packs only version-bumped projects with an `-alpha-<sha>`
suffix.

## Architecture

Understanding these four pieces explains most of the codebase.

**Transport selection by interface.** `ITransportChannelFactory.CanCreate(Type)` decides which
registered transport claims a message type. A message whose transport was never registered fails at
**host startup**, not at send time (`TransportStarterFactory`). Adding a transport means implementing
`ITransport`, `ITransportChannelFactory`, an `IMessageStateHandler<T>` mapping
complete/abandon/dead-letter onto the technology, a configuration type, a client, and marker
interfaces deriving from `ICommand`/`IEvent`. Polling transports derive their pump from
`GenericMessagePump`, which owns the prefetch and concurrency arithmetic — PostgreSQL is the most
recent example and the best template.

**Discovery is reflection over DI registrations.** Handlers are registered scoped, once per closed
processor interface plus once as `IGenericProcessor`; startup enumerates those to find processors.
Message-to-queue mappings are found by scanning the assembly **declaring the message type**
(`AutoMessageMapper`), which is why an `IMessageMapping<T>` must live beside its message. A processor
that is never registered is never discovered, and nothing warns you.

**The middleware pipeline has a fixed order**, built per listener in
`knightbus/src/KnightBus.Host/MiddlewarePipeline.cs`: in-flight tracker → error handling → DI scope provider →
dead-lettering → everything registered via `AddMiddleware` → the handler. Registration order only
affects that fifth group, so **user middleware runs innermost** and cannot observe exceptions that
`ErrorHandlingMiddleware` has already swallowed. Much of the framework is itself middleware —
attachments, sagas, throttling, tracing.

**Shutdown drains rather than waiting.** `KnightBusHost.StopAsync` stops fetching, polls in-flight
count every 100 ms up to `ShutdownGracePeriod` (default 30s), then cancels a *second* teardown token
to release singleton locks and stop `IStoppablePlugin`s. Locks are deliberately held through the
drain so a rolling deploy cannot overlap. If you change shutdown, `InFlightMessageTracker` is the
counter it depends on, and it must stay outermost in the pipeline.

## When documenting behaviour

The `knightbus/examples/*` projects are built by CI, so they are the authority on current API usage —
prefer their patterns over anything in prose, and fix an example if it contradicts the docs. When
writing docs, verify claims against source rather than inferring from names: several long-standing
assumptions turned out to be false (only the Blob saga store detects concurrent writes; the
PostgreSQL transport runs no message pre-processors at all, so attachments and outgoing trace
propagation silently do nothing there).
