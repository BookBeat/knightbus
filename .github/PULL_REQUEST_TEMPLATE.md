## What this changes

<!-- What the change does, and why. Link the issue if there is one. -->

## Notes for the reviewer

<!-- Anything worth knowing: a decision you went back and forth on, a limitation you accepted. -->

## Checklist

- [ ] `dotnet build KnightBus.slnx` and `dotnet test` pass
- [ ] `dotnet csharpier check .` passes (CI fails on unformatted code)
- [ ] Tests cover the change
- [ ] Documentation under `docs/` is updated, if the change is user-visible
- [ ] `<Version>` is bumped in the affected `.csproj` and `CHANGELOG.md` has an entry, if this
      should be released
