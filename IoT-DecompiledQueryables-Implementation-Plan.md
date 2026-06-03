# Implementation Plan — Complementary IoT Processing on DecompiledQueryables

## 1) Feasibility Assessment

**Feasibility Level: High**

This feature is feasible with limited and backward-compatible changes because DelegateDecompiler already centralizes query transformation in `DecompiledQueryProvider` and wraps queryables through `DecompiledQueryable<T>`. These are natural integration points for a post-decompile, pre-execution processing pipeline.

Favorable points:
- Existing decompilation pipeline is centralized in `CreateQuery<TElement>`, `Execute`, and `Execute<TResult>`.
- `DecompiledQueryable<T>` already controls runtime enumeration boundaries (`GetEnumerator`, `GetAsyncEnumerator`), making execution-time hooks possible.
- Public extension methods already expose fluent entry points via `DecompileExtensions`.

Constraints:
- Must preserve expression semantics and avoid changing translated SQL unless processors intentionally modify expressions.
- Must stay compatible with all target frameworks (.NET Framework 4.0/4.5, .NET Standard 2.0/2.1, .NET 8/9/10).
- Must avoid introducing allocations or hot-path overhead for users not using processors.

Feasibility conclusion:
- Achievable incrementally with test-first development and no API breaking changes.

## 2) Requirement Accuracy Assessment

**Requirement Accuracy: Good**

The requirement correctly identifies a real extension need: users want automatable, execution-adjacent custom processing over decompiled queryables. The intended insertion point (between decompilation and provider execution/translation) is coherent with DelegateDecompiler architecture.

Clarification retained for implementation:
- "IoT" is interpreted as complementary instrumentation/operations/tracing processing attached to decompiled query execution, not a device protocol feature.
- The first implementation should support deterministic, synchronous hooks over expression processing; async-specific hooks can be phase 2.

## 3) Recommended Solution

### 3.1 General Strategy

Use a **two-layer extensibility model** to match the roadmap execution points:
1. **Expression processing layer** in `DecompiledQueryProvider` (between decompile/optimize and provider translation/execution).
2. **Execution notification layer** in `DecompiledQueryable<T>` (`GetEnumerator` / `GetAsyncEnumerator`) and in provider `Execute` methods.

This separation is the best implementation path because expression rewriting and execution instrumentation have different responsibilities and safety constraints.

Default behavior remains unchanged when no extension is configured.

### 3.2 API Design

#### 3.2.1 Expression Processor Contract

Introduce:
- `IDecompiledQueryProcessor`
  - `Expression Process(Expression expression)`

Rules:
- Pure expression-in/expression-out contract.
- Null return is forbidden.
- Processor should be stateless or thread-safe.

#### 3.2.2 Execution Hook Contract

Introduce a dedicated non-transforming execution hook, for observability/automation:
- `IDecompiledQueryExecutionHook`
  - `void OnExecute<T>(DecompiledQueryable<T> queryable)`

Invocation points:
- `DecompiledQueryProvider.Execute` and `Execute<TResult>`.
- `DecompiledQueryable<T>.GetEnumerator`.
- `DecompiledQueryable<T>.GetAsyncEnumerator` (when available).

This hook is intentionally side-effect-only and does not alter expressions.

#### 3.2.3 Provider Pipeline Holder

Add immutable collections in `DecompiledQueryProvider`:
- processors (`IDecompiledQueryProcessor[]`)
- execution hooks (`IDecompiledQueryExecutionHook[]`)

Keep constructors backward compatible:
- Existing constructor delegates to empty arrays.
- New internal constructor(s) accept pipeline collections.

Helpers:
- `Expression ApplyProcessors(Expression expression)`
- `void NotifyExecute(Expression expression)`

#### 3.2.4 Fluent Extensions

Add fluent registration methods in `DecompileExtensions`:
- `WithProcessor<T>(this IQueryable<T> self, IDecompiledQueryProcessor processor)`
- `WithProcessing<T>(this IQueryable<T> self, Func<Expression, Expression> processor)`
- `WithExecutionHook<T>(this IQueryable<T> self, IDecompiledQueryExecutionHook hook)`
- `WithOnExecute<T>(this IQueryable<T> self, Action<Expression> hook)`

Behavior:
- If query already uses `DecompiledQueryProvider`, append to existing pipeline.
- Otherwise call `Decompile()` first, then append.
- Registration is query-scoped (no global mutable registry).

### 3.3 Execution Integration

Provider path:
- `CreateQuery<TElement>`: `decompile -> optimize -> apply processors -> inner.CreateQuery`
- `Execute` / `Execute<TResult>`: same sequence, then `NotifyExecute(finalExpression)` before forwarding to inner provider.

Queryable path:
- In `DecompiledQueryable<T>.GetEnumerator` and `GetAsyncEnumerator`, trigger provider execution notification for the current expression before delegating to `inner`.

This explicitly satisfies the roadmap requirement about execution-time invocation points.

### 3.4 Safety and Performance

- Zero additional processing cost when both pipelines are empty.
- No static mutable global registration in phase 1.
- Deterministic ordering for processors and hooks (registration order).
- Expression mutation remains isolated to processor layer only.

## 4) Architecture / Compatibility Impact

- No breaking changes required.
- New file(s):
  - `src/DelegateDecompiler/IDecompiledQueryProcessor.cs`
  - `src/DelegateDecompiler/IDecompiledQueryExecutionHook.cs`
  - optional internal adapters for delegate-based overloads.
- Modified files:
  - `src/DelegateDecompiler/DecompiledQueryProvider.cs`
  - `src/DelegateDecompiler/DecompiledQueryable.cs`
  - `src/DelegateDecompiler/DecompileExtensions.cs`
- Test files:
  - `src/DelegateDecompiler.Tests/QueryableExtensionsTests.cs` (or dedicated test class).
- Compatible with all target TFMs (no dependency beyond LINQ expressions and base BCL).

## 5) Detailed Implementation Plan

### Step 1 — Baseline test run

Run baseline suites first:
- `src/DelegateDecompiler.Tests`
- `src/DelegateDecompiler.Tests.VB`

Use `-p:DisableGitVersionTask=true` when needed.

### Step 2 — Red tests for expression processing

Add failing tests first for processors:
1. `WithProcessing_TransformsExpressionBeforeProviderExecution`
2. `WithProcessor_ChainsInRegistrationOrder`
3. `WithoutProcessor_BehaviorUnchanged`
4. `WithProcessing_AppliesAfterDecompileAndOptimize`

### Step 3 — Red tests for execution hooks

Add failing tests for execution callbacks:
1. `WithOnExecute_InvokedOnExecute`
2. `WithOnExecute_InvokedOnGetEnumerator`
3. `WithOnExecute_InvokedOnGetAsyncEnumerator_WhenSupported`
4. `WithOnExecute_DoesNotMutateExpression`

### Step 4 — Add contracts and adapters

Implement:
- `IDecompiledQueryProcessor`
- `IDecompiledQueryExecutionHook`
- optional internal adapters from delegates.

### Step 5 — Extend `DecompiledQueryProvider`

Implement immutable pipeline storage, constructor overloads, `ApplyProcessors`, and `NotifyExecute`.
Integrate calls into `CreateQuery<TElement>`, `Execute`, and `Execute<TResult>`.

### Step 6 — Extend `DecompiledQueryable<T>`

Wire execution notifications from `GetEnumerator` and `GetAsyncEnumerator` through the provider pipeline without changing query semantics.

### Step 7 — Add fluent extensions

Implement `WithProcessor`, `WithProcessing`, `WithExecutionHook`, and `WithOnExecute` in `DecompileExtensions` with support for both decompiled and non-decompiled inputs.

### Step 8 — XML documentation update

Add XML documentation for all new public/internal types and members (including internal types), matching repository preference.

### Step 9 — Incremental validation

Run targeted tests after each step (queryable extension tests + new tests).

### Step 10 — Final validation

Run required suites:
- `dotnet test -c Debug -f net8.0 src/DelegateDecompiler.Tests -p:DisableGitVersionTask=true`
- `dotnet test -c Debug -f net8.0 src/DelegateDecompiler.Tests.VB -p:DisableGitVersionTask=true`

## 6) Acceptance Criteria

- Users can attach expression processors and execution hooks fluently.
- Expression processors run between decompilation/optimization and provider execution.
- Execution hooks are invoked from `Execute`, `GetEnumerator`, and `GetAsyncEnumerator` (where available).
- Ordering is deterministic and query-scoped.
- Existing behavior is unchanged when no extension is configured.
- Core test suites pass with no regression.
