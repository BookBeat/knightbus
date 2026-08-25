# Contributing

KnightBus is developed at [BookBeat/knightbus](https://github.com/BookBeat/knightbus) and released
under the MIT licence.

## Repository layout

```
src/        One folder per published package
tests/      One folder per test project, plus the shared integration base classes
samples/    Runnable example applications
docs/       The documentation site, and the brand assets
```

Every project folder is named after the project it contains, and the project file name is the
published NuGet package id — nothing sets `PackageId` or `AssemblyName`. Renaming a folder under
`src/` therefore renames a public package.

> **Note**
> `docs/assets/images` is used by the build, not just by the site. `Directory.Build.props` packs
> `docs/assets/images/knighbus-64.png` as the `PackageIcon` for every package, and the root
> `README.md` uses the logo from the same folder. Renaming or removing those files fails
> `dotnet pack` with `NU5046`.

## Building and testing

```bash
dotnet build KnightBus.slnx
dotnet test
```

The integration suites start their own dependencies with
[Testcontainers](https://dotnet.testcontainers.org), so Docker needs to be running, but you do not
need to start anything yourself.

## Dependencies

Package versions are managed centrally in `Directory.Packages.props`, so a `PackageReference` in a
`.csproj` carries only the package id. The framework packages that ship with the runtime are pinned
per target framework, in the conditional item groups near the top of that file.

Settings shared by all test projects live in `tests/Directory.Build.props` and
`tests/Directory.Build.targets`; the samples share `samples/Directory.Build.props`. Each of those
chains the repository root `Directory.Build.props` explicitly, because MSBuild stops walking up at
the first file it finds.

## Formatting

Formatting is enforced by [CSharpier](https://csharpier.com) and checked in CI, so an unformatted
change fails the build.

```bash
dotnet tool restore
dotnet csharpier format .
```

The repository ships a pre-commit hook that runs the formatter. Point git at it once:

```bash
git config core.hooksPath .githooks
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

> **Note**
> Keep `<Version>` in the csproj. The pre-release workflow decides what to publish by reading the
> `<Version>` element out of each `.csproj` and comparing it against the branch point, matching
> projects by file name so that moving one is not mistaken for a release. Moving versions into a
> shared props file would break that detection — and it would fail silently, publishing nothing.

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
[samples](https://github.com/BookBeat/knightbus/tree/master/samples) — they are compiled by CI, so
they cannot drift from the current API the way prose can.

## Adding a transport

A transport implements `ITransport` and `ITransportChannelFactory`, plus a message marker interface
deriving from `ICommand`/`IEvent`, a configuration type implementing `ITransportConfiguration`, a
client, and an `IMessageStateHandler<T>` that maps completion, abandonment and dead-lettering onto
the underlying technology.

For a polling transport, derive the pump from `GenericMessagePump`, which handles the prefetch and
concurrency arithmetic. The PostgreSQL transport is the most recent example and a good template.
