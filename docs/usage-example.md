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

Start with a small subset and widen later if the results stay useful.

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
