# Usage Example

Use these examples when consuming `Meridian.Analyzer`.

## Install The Package

Add the package to a project:

```bash
dotnet add package Meridian.Analyzer
```

## Configure Severity

Enable rules in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MER0001.severity = warning
dotnet_diagnostic.MER0002.severity = warning
```

Start with a small subset and widen later if the signal stays clean.

## Build Validation

Run a normal build on the consuming project:

```bash
dotnet build
```

Analyzer diagnostics will surface according to the severities configured by the consumer.

## Direct Package Reference

```xml
<ItemGroup>
  <PackageReference Include="Meridian.Analyzer" Version="0.2.*" PrivateAssets="all" />
</ItemGroup>
```

## Local Maintainer Checks

When editing this repository itself:

```bash
dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release
dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts
```
