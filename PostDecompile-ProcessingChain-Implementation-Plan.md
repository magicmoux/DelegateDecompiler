# Implementation Plan — Complementary Processing IoT (Post-Decompilation)

## 1) Feasibility Assessment

**Feasibility Level: High to Very High**

The feature is realistic because the pipeline is centralised in `DecompiledQueryProvider`:
- decompilation (`expression.Decompile()`), then
- optimisation (`Optimize()`), then
- execution via the inner provider.

It is therefore possible to insert a custom processing hook between these steps without reworking the IL engine.

## 2) Requirement Accuracy Assessment

**Requirement Accuracy: Good on the objective; API proposal needs adjustment**

The requirement is relevant: allow customisation of the expression tree before ORM translation.

Nuance on the `Action<>` proposal:
- A plain `Action<Expression>` is limited (no return value, requires non-idiomatic external mutation).
- For a transformation chain, a `Func<Expression, Expression>` form is safer and composable.

Recommendation:
- Expose a transformation-oriented overload (`Func<Expression, Expression>`), while optionally accepting an `Action<Expression>` version converted internally to a no-op transform if desired for ergonomics.

## 3) Recommended Solution

### 3.1 Public API

Add non-breaking overloads in `DecompileExtensions`:
- `IQueryable<T> Decompile<T>(this IQueryable<T> self, Func<Expression, Expression> postProcessor)`
- `IQueryable Decompile(this IQueryable self, Func<Expression, Expression> postProcessor)`

Optional ergonomics:
- `Action<Expression>` supported via an internal wrapper, but secondary.

### 3.2 Provider Integration

Extend `DecompiledQueryProvider` to store an optional post-processor and apply it after `Decompile().Optimize()` (or between the two, depending on the product objective).

Recommended pipeline ordering (default):
1. Decompile
2. Optimize
3. Custom post-process

Rationale: the user receives an already normalised base expression tree, which is more stable to manipulate.

### 3.3 Advanced Alternative (Phase 2)

If needed, introduce pipeline options to choose the hook position (before/after optimise), but defer to limit initial complexity.

## 4) Architecture / Compatibility Impact

- Compatible across all TFMs: `Func<Expression, Expression>` is available on every targeted TFM.
- No breakage of existing signatures (overloads only).
- Thread-safety: avoid shared mutable state; store the delegate in the provider instance.

## 5) Detailed Implementation Plan

### Step 1 — Red API tests in `DelegateDecompiler.Tests`

Add test cases for the new `Decompile` overloads verifying:
- The overload compiles and is callable.
- A provided callback receives the decompiled expression.
- The expression returned by the callback is the one forwarded to the inner provider.

### Step 2 — Implement `DecompileExtensions` overloads

Add to `DecompileExtensions`:

```csharp
public static IQueryable<T> Decompile<T>(
    this IQueryable<T> self,
    Func<Expression, Expression> postProcessor)
{
    if (postProcessor == null) throw new ArgumentNullException(nameof(postProcessor));
    return self.Provider is DecompiledQueryProvider
        ? self
        : new DecompiledQueryable<T>(new DecompiledQueryProvider(self.Provider, postProcessor), self.Expression);
}

public static IQueryable Decompile(
    this IQueryable self,
    Func<Expression, Expression> postProcessor)
{
    if (postProcessor == null) throw new ArgumentNullException(nameof(postProcessor));
    return self.Provider is DecompiledQueryProvider
        ? self
        : new DecompiledQueryable(new DecompiledQueryProvider(self.Provider, postProcessor), self.Expression);
}
```

### Step 3 — Adapt `DecompiledQueryProvider`

Add an optional `Func<Expression, Expression> _postProcessor` field.
Extend the constructor to accept it (existing constructor passes `null` — no behaviour change).

In the expression execution path (where `Optimize` is called), apply the post-processor if non-null:

```csharp
var optimized = OptimizeExpressionVisitor.Optimize(decompiled);
var processed = _postProcessor != null ? _postProcessor(optimized) : optimized;
// proceed with processed
```

### Step 4 — Robustness tests

- Null callback → `ArgumentNullException`.
- Callback returns `null` → explicit `InvalidOperationException` with a descriptive message.
- Callback returns the expression unchanged → behaviour identical to standard `Decompile()`.

### Step 5 — Non-regression tests

Verify that all existing `Decompile()` calls (without callback) produce strictly identical results.

### Step 6 — Full validation

```bash
dotnet test -c Debug -f net8.0 src/DelegateDecompiler.Tests -p:DisableGitVersionTask=true
dotnet test -c Debug -f net8.0 src/DelegateDecompiler.Tests.VB -p:DisableGitVersionTask=true
dotnet test -c Debug -f net8.0 src/DelegateDecompiler.EntityFrameworkCore8.Tests -p:DisableGitVersionTask=true
```

## 6) Acceptance Criteria

- The new overloads allow a custom transformation to be plugged in.
- The transformation is applied deterministically before provider execution.
- No historical behaviour is changed in the absence of a callback.
- Build and targeted test suites pass.

## 7) Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| User callback builds an expression invalid for the ORM provider | Clearly document the contract; validate non-null return; throw explicit exceptions |
| Pipeline ordering not suited to some scenarios | Start with a fixed documented order; open a configurable phase 2 if real need arises |
| Perf overhead | Callback is optional; zero cost by default (null check only) |