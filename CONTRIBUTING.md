# Contributing to Process Bus Insight

Thank you for helping improve Process Bus Insight.

This project is intended to stay receive-only, raw-passive, evidence-focused, and honest about timing confidence.

## Good contribution areas

- SV, GOOSE, and PTP decoder coverage.
- SCL examples and parser improvements.
- Validation scenarios for FAT/SAT workflows.
- UI clarity and evidence wording.
- Documentation, troubleshooting notes, and field checklists.
- Automated tests for protocol parsers and edge cases.

## Product boundaries

Please keep these boundaries intact:

- Do not add IEC 61850 control workflows to the product app.
- Do not make timing claims beyond the timestamp source quality.
- Do not vendor restricted third-party binaries without license review.
- Do not add sample captures that expose private customer/project data.

## Pull request checklist

Before opening a pull request:

1. Build the solution in Release mode.
2. Confirm the app still starts on Windows.
3. Keep README and docs clear for engineers using the application.
4. Add or update validation notes when behavior changes.
5. Avoid committing `bin`, `obj`, `artifacts`, captures, logs, or local settings.

## Development setup

```powershell
git clone https://github.com/masarray/DigSubAnalyzer.git
cd DigSubAnalyzer
dotnet restore .\ProcessBusSuite.sln
dotnet build .\ProcessBusSuite.sln -c Release
```
