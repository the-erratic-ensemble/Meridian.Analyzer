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
dotnet_diagnostic.MER0023.severity = warning
dotnet_diagnostic.MER0036.severity = warning
dotnet_diagnostic.MER0037.severity = warning
dotnet_diagnostic.MER0038.severity = warning
dotnet_diagnostic.MER0039.severity = warning
dotnet_diagnostic.MER0040.severity = warning
dotnet_diagnostic.MER0041.severity = warning
dotnet_diagnostic.MER0042.severity = warning
dotnet_diagnostic.MER0043.severity = warning
dotnet_diagnostic.MER0044.severity = warning
dotnet_diagnostic.MER0045.severity = warning
dotnet_diagnostic.MER0046.severity = warning
dotnet_diagnostic.MER0047.severity = warning
dotnet_diagnostic.MER0048.severity = warning
dotnet_diagnostic.MER0049.severity = warning
dotnet_diagnostic.MER0050.severity = warning
dotnet_diagnostic.MER0051.severity = warning
dotnet_diagnostic.MER0052.severity = warning
dotnet_diagnostic.MER0053.severity = warning
dotnet_diagnostic.MER0054.severity = warning
dotnet_diagnostic.MER0055.severity = warning
dotnet_diagnostic.MER0056.severity = warning
dotnet_diagnostic.MER0057.severity = warning
dotnet_diagnostic.MER0058.severity = warning
dotnet_diagnostic.MER0059.severity = warning
dotnet_diagnostic.MER0060.severity = warning
```

Start with a small subset and widen later if the results stay useful.

## Build Validation

Run a normal build on the consuming project:

```bash
dotnet build
```

Analyzer diagnostics will surface according to the severities configured by the consumer.

## Rule Examples

### MER0023: Observe Task.Run Work

```csharp
var first = Task.Run(() => RenderFirst());
var second = Task.Run(() => RenderSecond());
await Task.WhenAll(first, second);
```

### MER0036: Choose String Ordering Semantics

```csharp
var ordered = values.OrderBy(value => value.Code, StringComparer.Ordinal);
```

### MER0037: Return ArrayPool Rentals

```csharp
var buffer = ArrayPool<byte>.Shared.Rent(size);
try {
    Use(buffer);
}
finally {
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### MER0038: Release SemaphoreSlim Capacity

```csharp
await gate.WaitAsync(cancellationToken);
try {
    await WorkAsync(cancellationToken);
}
finally {
    gate.Release();
}
```

### MER0039: Bind Commands to Transactions

```csharp
using var transaction = connection.BeginTransaction();
using var command = connection.CreateCommand();
command.Transaction = transaction;
command.CommandText = sql;
command.ExecuteNonQuery();
```

### MER0040: Clone Escaping JsonElement Values

```csharp
using var document = JsonDocument.Parse(payload);
return document.RootElement.Clone();
```

### MER0041: Model Literal Null State

```csharp
var empty = default(FixtureSlot<Club>);
```

### MER0042: Name Boolean Arguments

```csharp
ReadCompetitionFixtures(lease, clubId, completed: false, cancellationToken);
```

### MER0043: Order Before Positional Selection

```csharp
var role = roles.Keys
    .OrderBy(role => role, StringComparer.Ordinal)
    .First();
```

### MER0044: State String Equality

```csharp
var values = names.Distinct(StringComparer.Ordinal).ToArray();
```

### MER0045: Preserve Cancellation

```csharp
try {
    await WorkAsync(cancellationToken);
}
catch (OperationCanceledException) {
    throw;
}
catch (Exception exception) {
    LogFailure(exception);
}
```

### MER0046: Run Continuations Asynchronously

```csharp
var signal = new TaskCompletionSource(
    TaskCreationOptions.RunContinuationsAsynchronously);
```

### MER0047: Invoke Callbacks Outside Locks

```csharp
lock (_gate)
    snapshot = _state;

notify?.Invoke(snapshot);
```

### MER0048: Branch on Exception Identity

```csharp
if (exception is TimeoutException)
    retry = true;
```

### MER0049: Compare Array Contents Explicitly

```csharp
var equal = left.AsSpan().SequenceEqual(right);
```

### MER0050: Keep Runtime Hashing in Equality Code

```csharp
public override int GetHashCode() => HashCode.Combine(Id, Name);
```

### MER0051: Keep Async Callbacks Awaitable

```csharp
Func<IReadOnlyList<Record>, Task> callback = async records => await SaveAsync(records);
```

### MER0052: Enumerate a Snapshot Before Mutation

```csharp
foreach (var item in items.ToArray())
    items.Remove(item);
```

### MER0053: Bound Signed Values Before Modulo

```csharp
var offset = PositiveRemainder(subject, cadenceDays);
```

### MER0054: Consume TryGetValue Results

```csharp
if (values.TryGetValue(key, out var value))
    Use(value);
```

### MER0055: State Binary Byte Order

```csharp
var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
```

### MER0056: Validate Ordinary Enum Values

```csharp
if (Enum.TryParse<Mode>(text, true, out var mode) && Enum.IsDefined(mode))
    return mode;
```

### MER0057: State Midpoint Rounding

```csharp
var rounded = Math.Round(value, MidpointRounding.ToEven);
```

### MER0058: Bound Variable Stackalloc

```csharp
Span<byte> buffer = byteCount <= 1024
    ? stackalloc byte[byteCount]
    : new byte[byteCount];
```

### MER0059: Guard Search Results

```csharp
var index = text.IndexOf(marker, StringComparison.Ordinal);
if (index < 0)
    return null;

return text[index..];
```

### MER0060: Slice the Written MemoryStream Range

```csharp
var bytes = stream.GetBuffer().AsSpan(0, checked((int)stream.Length));
```

## Direct Package Reference

```xml
<ItemGroup>
  <PackageReference Include="Meridian.Analyzer" Version="0.5.*" PrivateAssets="all" />
</ItemGroup>
```
