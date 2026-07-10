## Engineering intent

Describe the FAT/SAT, commissioning, interoperability, troubleshooting, or repository-quality problem addressed by this change.

## What changed

- 

## Validation

- [ ] `dotnet restore .\ProcessBusSuite.sln`
- [ ] `dotnet build .\ProcessBusSuite.sln -c Release --no-restore`
- [ ] `dotnet test .\ProcessBusSuite.sln -c Release --no-build`
- [ ] `pwsh .\scripts\repository-health.ps1`
- [ ] Runtime smoke test on Windows, when runtime behavior changed
- [ ] Documentation/screenshots updated when user-visible behavior changed

## Safety and evidence

- [ ] Receive-only product boundary is preserved
- [ ] Timing language matches timestamp-source confidence
- [ ] No customer, site, device, MAC/IP, capture, SCL, or project-sensitive data is included
- [ ] No `bin`, `obj`, `artifacts`, logs, captures, or local settings are committed

## Compatibility / migration notes

None, or describe them here.
