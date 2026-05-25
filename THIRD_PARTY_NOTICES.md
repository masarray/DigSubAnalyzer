# Third-Party Notices

Process Bus Insight currently uses .NET / WPF platform assemblies and direct Npcap `wpcap` interop.

## Source Dependencies

- No external NuGet package dependency is declared by the product projects at this time.
- The product runtime is raw-passive and does not link to `libiec61850` or an IEC 61850 subscriber stack.

## Runtime Requirements

- Npcap is required on the target Windows machine for raw packet capture. Npcap is not vendored in this repository.
- Self-contained publish artifacts include Microsoft .NET runtime/WPF files produced by `dotnet publish`. Review Microsoft .NET runtime notices before redistributing binary packages outside internal R&D preview use.

## Project License

Project source code is prepared for GPL-3.0-or-later distribution. See `LICENSE`.
