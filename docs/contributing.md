# Contributing

KnightBus is developed at [BookBeat/knightbus](https://github.com/BookBeat/knightbus) and released
under the MIT licence.

## Repository layout

The repository is a merged multi-repo. The framework lives under `knightbus/`, and each transport or
integration has its own top-level folder:

```
knightbus/                  Core, Host, Messages, Core.Management, tests, examples
knightbus-azureservicebus/  Azure Service Bus transport
knightbus-azurestorage/     Azure Storage Queues transport
knightbus-postgresql/       PostgreSQL transport
knightbus-redis/            Redis transport
knightbus-nats/             NATS transport
knightbus-sqlserver/        SQL Server saga store
knightbus-schedule/         Cron scheduling
knightbus-*/                Monitoring and serialization integrations
docs/                       This documentation site, and the brand assets
```

!!! note "`docs/assets/images` is used by the build, not just the site"
    `Directory.Build.props` packs `docs/assets/images/knighbus-64.png` as the `PackageIcon` for every
    package, and the root `README.md` uses the logo from the same folder. Renaming or removing those
    files fails `dotnet pack` with `NU5046`.

Every publishable project sits at `<area>/src/<Package.Name>/`, with tests at `<area>/tests/`.

## Building and testing

```bash
dotnet build KnightBus.slnx
dotnet test
```

Some integration tests need backing services. CI starts them with Docker, and you can do the same:

```bash
docker run -d -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
docker run -d -p 4222:4222 nats:latest
```

## Formatting

Formatting is enforced by [CSharpier](https://csharpier.com) and checked in CI, so an unformatted
change fails the build.

```bash
dotnet tool restore
dotnet csharpier format .
```

The repository ships a `pre-commit` hook that runs the formatter. Install it once:

```bash
cp pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

## Versioning and releasing

KnightBus follows [Semantic Versioning](https://semver.org), **per package**. Packages are versioned
and released independently, so a breaking change in one transport does not bump everything else.

Shared package metadata (project URL, licence, icon, authors) lives in the root
`Directory.Build.props`. Only per-package values — `Version`, `Description`, `PackageTags`,
`TargetFrameworks` — belong in the individual `.csproj`.

To release a package:

1. Bump `<Version>` in that project's `.csproj`.
2. Add an entry to `CHANGELOG.md` (and to the package's own changelog where it has one).
3. Merge to `master`.

CI packs the solution and pushes to NuGet with `--skip-duplicate`, so only projects whose version
actually changed are published.

!!! note "Keep `<Version>` in the csproj"
    The pre-release workflow decides what to publish by diffing `<Version>` elements in `.csproj`
    files against `master`. Moving versions into a shared props file would break that detection.

### Pre-release packages

The **Publish Pre-Release Packages (Manual)** workflow is triggered from the Actions tab. It packs
only the projects whose version changed relative to `master` and publishes them with an
`-alpha-<sha>` suffix, which is the way to get a build in front of a consuming application before
merging.

## Documentation

The site is [MkDocs Material](https://squidfunk.github.io/mkdocs-material/), with sources in `docs/`
and configuration in `mkdocs.yml`.

```bash
python -m venv .venv && source .venv/bin/activate
pip install -r docs/requirements.txt
mkdocs serve
```

`mkdocs serve` gives you a live-reloading preview on <http://127.0.0.1:8000>. Before pushing, run the
same build CI runs:

```bash
mkdocs build --strict
```

`--strict` turns warnings into errors, so a broken internal link or a page missing from the navigation
fails the build.

Pushes to `master` that touch `docs/` or `mkdocs.yml` deploy the site to GitHub Pages automatically;
pull requests build it without deploying.

When documenting an API, treat the code as the source of truth and prefer patterns from the
[examples](https://github.com/BookBeat/knightbus/tree/master/knightbus/examples) — they are compiled
by CI, so they cannot drift from the current API the way prose can.

## Adding a transport

A transport implements `ITransport` and `ITransportChannelFactory`, plus a message marker interface
deriving from `ICommand`/`IEvent`, a configuration type implementing `ITransportConfiguration`, a
client, and an `IMessageStateHandler<T>` that maps completion, abandonment and dead-lettering onto
the underlying technology.

For a polling transport, derive the pump from `GenericMessagePump`, which handles the prefetch and
concurrency arithmetic. The PostgreSQL transport is the most recent example and a good template.
