# Implementation Plan — Native mutual exclusivity of XOR clauses Optimisation

## 1) Feasibility Assessment

**Feasibility Level: High**

This feature is technically achievable without major refactoring, as the project already includes an expression optimisation pass in `OptimizeExpressionVisitor` (applied after decompilation). The natural insertion point is an additional rule within `VisitBinary`, alongside the existing optimisations (logical inversion, TypeAs simplification, etc.).

Favourable points:
- The current pipeline already performs boolean simplifications (`VisitConditional`, `VisitBinary`, `VisitUnary`, logical inversion, etc.).
- The targeted transformation (`XOR` between two mutually exclusive members) can be applied **conservatively** with safety guards.
- The static method `Optimize(Expression)` at the bottom of `OptimizeExpressionVisitor` is the sole entry point: no public API surface breakage.
- The `expressionsCache` mechanism already present in `Visit()` amortises the cost of re-visits.

Constraints:
- Compiler-generated forms may vary (direct `ExclusiveOr`, or an equivalent unfolded into `Conditional/AndAlso/OrElse`).
- An overly aggressive rule could introduce incorrect rewrites.

Feasibility conclusion:
- Achievable in short iterations with incremental test coverage.
- Primary risk is manageable through strict pattern matching.

## 2) Requirement Accuracy Assessment

**Requirement Accuracy: Good, with one important nuance**

The requirement correctly identifies the business problem: in cases of logical mutual exclusion, `A ^ B` is equivalent to `A || B`, and the OR form leads to terser translations by removing redundant exclusivity checks in the LINQ/ORM providers resulting query.

Necessary nuance:
- The equivalence `A ^ B <=> A || B` holds **only if** `A` and `B` are mutually exclusive (at most one can be `true` simultaneously).
- The implementation plan must therefore target **recognised structural patterns** (not a general XOR→OR rule).
- Equivalence verification cannot be general at a reasonable cost: it is **pattern-matching based** on the expression tree structure, following the rules described below.

## 3) Recommended Solution

### 3.1 General Strategy

Add a call to a new static method `TryOptimizeMutuallyExclusiveXor` inside `VisitBinary` of `OptimizeExpressionVisitor` that:
1. Filters on `ExpressionType.ExclusiveOr` only.
2. Delegates mutual exclusion detection to a set of **declarative registered rules**.
3. If a rule recognises the pattern, rewrites to `Expression.OrElse(left, right)`.
4. Otherwise passes the expression through unchanged.

### 3.2 Pattern Extension Mechanism (detailed design)

#### 3.2.1 Rule Interface

Each rule is a delegate matching the following signature:

```csharp
// Returns true if left and right are mutually exclusive according to this rule.
delegate bool MutualExclusionRule(Expression left, Expression right);
```

The full set of rules is stored in a static immutable array inside `OptimizeExpressionVisitor`:

```csharp
static readonly Func<Expression, Expression, bool>[] MutualExclusionRules =
{
    IsAnyContainsMutuallyExclusive,
    // add future rules here
};
```

**Adding a rule = adding one static method + registering it in this array.**
No other file needs to be modified.

#### 3.2.2 Equivalence Verification: Normalisation Principle

Before applying any rule, both sides of the XOR are **normalised**:

```
strip(expr):
  if expr is Not(inner) → return (inner, negated=true)
  otherwise             → return (expr,  negated=false)
```

A pair `(left, right)` is mutually exclusive if, after normalisation:
- `stripL` and `stripR` satisfy a rule **and** their polarities `(negatedL, negatedR)` match the pattern expected by the rule.

This allows a single rule to cover both `!A ^ B` and `A ^ !B` without duplication.

#### 3.2.3 Phase 1 Rule: `Any` / `Contains` on the Same Source

**Semantics**: `!coll.Any() ^ coll.Contains(x)` is mutually exclusive because:
- If the collection is empty, `!coll.Any()` is `true` and `coll.Contains(x)` is necessarily `false`.
- If the collection contains `x`, `coll.Contains(x)` is `true` and `coll.Any()` is `true`, so `!coll.Any()` is `false`.

Recognised pattern (after normalisation):

| `stripL`           | `negatedL` | `stripR`           | `negatedR` | Valid? |
|--------------------|------------|--------------------|------------|--------|
| `coll.Any()`       | `true`     | `coll.Contains(x)` | `false`    | Yes    |
| `coll.Contains(x)` | `false`    | `coll.Any()`       | `true`     | Yes (reversed order, covered by symmetry) |
| `coll.Any()`       | `false`    | `coll.Contains(x)` | `true`     | Yes    |
| `coll.Any()`       | `false`    | `coll.Contains(x)` | `false`    | No — mutual exclusion not guaranteed |

Strict matching criteria for the rule:
1. `stripL` is a `MethodCallExpression` with `Method.Name == "Any"` and no predicate argument.
2. `stripR` is a `MethodCallExpression` with `Method.Name == "Contains"` with exactly one element argument.
3. The **source** is the same: `ExpressionsAreEqual(anyCall.Object ?? anyCall.Arguments[0], containsCall.Object ?? containsCall.Arguments[0])`.
4. Exactly one side is negated (`negatedL XOR negatedR == true`).

#### 3.2.4 Structural Expression Comparer

Source identity verification requires a comparer:

```csharp
static bool ExpressionsAreEqual(Expression a, Expression b)
    => ReferenceEquals(a, b) || a.ToString() == b.ToString();
```

`ToString()` is sufficient in phase 1 as source expressions are typically simple parameters or fields. A full structural comparison can be deferred to phase 2.

### 3.3 Phase 2 Extensions

After phase 1 validation:
- Extend rules to cover more destructured compiler forms (`Conditional` unfolded into implicit XOR).
- Add a `Count() == 0 ^ Contains()` rule if `Count==0 → !Any()` normalisation is not already performed elsewhere.
- Consider a full `ExpressionEqualityComparer` (to be shared with other optimisers).

## 4) Architecture / Compatibility Impact

- No public API breakage.
- Modified files: only `src/DelegateDecompiler/OptimizeExpressionVisitor.cs`.
- New file: `src/DelegateDecompiler.Tests/XorOptimizationTests.cs`.
- Compatible with all current TFMs (`.NET Framework 4.0/4.5`, `.NET Standard`, `.NET 8/9/10`) as it relies solely on `System.Linq.Expressions`.
- Negligible perf risk: the rule list is short, the call is triggered only on `ExclusiveOr`, and the `expressionsCache` in `Visit()` applies.

## 5) Detailed Implementation Plan

### Step 1 — Red tests in `src/DelegateDecompiler.Tests/XorOptimizationTests.cs`

Create the class `XorOptimizationTests : DecompilerTestsBase` with the following cases:

```csharp
// Recognised cases → expected: OrElse in the decompiled body
[Test] void Test_NotAny_Xor_Contains_RewritesToOrElse()
[Test] void Test_Any_Xor_NotContains_RewritesToOrElse()
[Test] void Test_OrderInvariant_Contains_Xor_NotAny_RewritesToOrElse()

// Unrecognised cases → expected: ExclusiveOr preserved
[Test] void Test_Any_Xor_Contains_NoRewrite_NotMutuallyExclusive()
[Test] void Test_DifferentSources_NotRewritten()
[Test] void Test_UnrelatedXor_NotRewritten()
```

Each case uses `Test(compiled, expected)` with lambdas over `IList<int>` to target standard LINQ overloads.
Positive cases assert `decompiled.Body.NodeType == ExpressionType.OrElse`.
Negative cases assert `decompiled.Body.NodeType == ExpressionType.ExclusiveOr`.

### Step 2 — Implementation in `OptimizeExpressionVisitor`

**2a. `StripNot` utility method**
```csharp
static Expression StripNot(Expression expr, out bool negated)
{
    if (expr.NodeType == ExpressionType.Not)
    { negated = true; return ((UnaryExpression)expr).Operand; }
    negated = false;
    return expr;
}
```

**2b. Minimal source comparer**
```csharp
static bool ExpressionsAreEqual(Expression a, Expression b)
    => ReferenceEquals(a, b) || a.ToString() == b.ToString();
```

**2c. LINQ source extractor**
```csharp
static bool TryGetLinqSource(MethodCallExpression call, out Expression source)
{
    source = call.Object ?? (call.Arguments.Count >= 1 ? call.Arguments[0] : null);
    return source != null;
}
```

**2d. `IsAnyContainsMutuallyExclusive` rule**

```csharp
static bool IsAnyContainsMutuallyExclusive(Expression rawLeft, Expression rawRight)
{
    var left  = StripNot(rawLeft,  out bool negL);
    var right = StripNot(rawRight, out bool negR);
    if (negL == negR) return false;   // exactly one must be negated

    var lc = left  as MethodCallExpression;
    var rc = right as MethodCallExpression;
    if (lc == null || rc == null) return false;

    MethodCallExpression anyCall, containsCall;
    bool anyNegated;
    if (IsParameterlessAny(lc) && IsContains(rc))
        { anyCall = lc; containsCall = rc; anyNegated = negL; }
    else if (IsParameterlessAny(rc) && IsContains(lc))
        { anyCall = rc; containsCall = lc; anyNegated = negR; }
    else return false;

    // Any() must be the negated side for mutual exclusion to hold
    if (!anyNegated) return false;

    return TryGetLinqSource(anyCall, out var s1)
        && TryGetLinqSource(containsCall, out var s2)
        && ExpressionsAreEqual(s1, s2);
}

static bool IsParameterlessAny(MethodCallExpression c)
    => c.Method.Name == "Any"
    && (c.Object != null ? c.Arguments.Count == 0 : c.Arguments.Count == 1);

static bool IsContains(MethodCallExpression c)
    => c.Method.Name == "Contains"
    && (c.Object != null ? c.Arguments.Count == 1 : c.Arguments.Count == 2);
```

**2e. Rule table and dispatcher**
```csharp
static readonly Func<Expression, Expression, bool>[] MutualExclusionRules =
{
    IsAnyContainsMutuallyExclusive,
};

static bool TryOptimizeMutuallyExclusiveXor(Expression left, Expression right, out Expression result)
{
    if (left.Type == typeof(bool))
        foreach (var rule in MutualExclusionRules)
            if (rule(left, right)) { result = Expression.OrElse(left, right); return true; }
    result = null;
    return false;
}
```

**2f. Hook in `VisitBinary`** (after the `TryOptimizeTypeAsComparison` block)
```csharp
if (node.NodeType == ExpressionType.ExclusiveOr
    && TryOptimizeMutuallyExclusiveXor(left, right, out var xorResult))
    return xorResult;
```

### Step 3 — Non-regression tests

Verify that negative cases are not rewritten: `decompiled.Body.NodeType == ExpressionType.ExclusiveOr`.

### Step 4 — Full validation

```bash
dotnet test -c Debug -f net8.0 src/DelegateDecompiler.Tests -p:DisableGitVersionTask=true
dotnet test -c Debug -f net8.0 src/DelegateDecompiler.Tests.VB -p:DisableGitVersionTask=true
```

## 6) Acceptance Criteria

- Expressions matching the target patterns are rewritten to `OrElse`.
- No functional behaviour change on existing tests.
- Unrecognised patterns retain an `ExclusiveOr` node.
- Solution compiles across all targeted TFMs.
- The `DelegateDecompiler.Tests` and `DelegateDecompiler.Tests.VB` suites pass in full.

## 7) Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| False positive on mutual exclusion (rule bug) | Explicit negative tests; `IsParameterlessAny` verifies absence of predicate argument |
| Unhandled compiler forms (`Conditional` unfolded) | Phased delivery; progressive extension of the `MutualExclusionRules` array |
| Sources not comparable via `ToString()` | Acceptable in phase 1; replace with structural comparer in phase 2 |
| Regression on non-boolean XOR (int, enum) | Guard `left.Type == typeof(bool)` in `TryOptimizeMutuallyExclusiveXor` |