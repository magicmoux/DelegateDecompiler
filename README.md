# ExpressionFactory (JIT) — DelegateDecompiler

This branch demonstrates PoC of a JIT-oriented extension point called `ExpressionFactory` that lets library authors provide extension methods which produce `System.Linq.Expressions.Expression` values dynamically generated at runtime. 
Factories may help you generate complex or dynamic behaviors as Linq Expressions that can still be used with DelegateDecompiler through an existing DelegateQueryable and stay provider-friendly (such as EntityFramework though still restricted by the provider limitations of course)

This README documents the ExpressionFactory feature, where the JIT helpers live, usage patterns, limitations and test guidance.
For general project usage and other components consult the main branch README or the original repository this one is forked from.

## Foreword

- The JIT code is still in development, use-cases and unit tests are most welcome if you're interested.
- See .github/copilot-instructions.md for test-first development guidelines.

## Overview

- Mark a method with `[ExpressionFactory]` that returns an `Expression` constructed from the call-site arguments.
- You can additionaly define methods or properties marked with `[Decompile]`/`[Computed]` to syntax-sugar integration of your factories into Linq queries.
- Before a query is executed or enumerated, the JIT visitor attempts to materialize factory arguments (constants, quoted lambdas, captured values, arrays, lists, etc.), convert them to the factory parameter types when possible, and invoke the factory method to obtain an `Expression` to splice into the caller's expression tree.
- This enables library authors to expose concise call-site APIs while still producing expressions that ORM providers can translate into SQL or other backend queries.

## Key attributes and API

- `[ExpressionFactory]` — annotate methods that build `Expression` instances.

Factory method example:

This example demonstrates how to sort an IQueryable or an ICollection in an explicit order against a specified entities' property.

For instance, you could sort and paginate a list of your repository's articles against pertinence returned by external FTS engine queries.

```csharp
[ExpressionFactory]
private static Expression BuildOrderBySequenceExpression<T, TKey>(
    Expression source,
    Expression<Func<T, TKey>> keySelector,
    IEnumerable<TKey> orderedKeys)
    where T : class
    where TKey : IEquatable<TKey>
{
    // return a Queryable.OrderBy / Enumerable.OrderBy call expression
}
```

Call site example:

```csharp
var q = db.EfParents
    .Select(p => p.Children.OrderBySequence(c => c.EfChildId, new[] { 2, 3, 1 }).FirstOrDefault());

// Just before execution the ExpressionFactory visitor will attempt to expand the factory
// so the resulting expression tree contains provider-translatable nodes.
```

## Where the implementation lives

- `src/DelegateDecompiler/JIT/ExpressionFactoryVisitor.cs` — walks expressions and expands factory calls when possible.
- `src/DelegateDecompiler/JIT/ExpressionFactoryArgumentHelper.cs` — tries to extract and convert call-site arguments to runtime values or typed `Expression<>` instances.
- `src/DelegateDecompiler/JIT/ExpressionFactoryInvoker.cs` — invokes the factory method via reflection and performs simple conversions (constructor from enumerable, ToArray conversion, etc.).
- `src/DelegateDecompiler/ExpressionExtensions.cs` and `src/DelegateDecompiler/MethodBodyDecompiler.cs` — integration points used by the visitor pipeline.

## How it works (high level)

1. The ExpressionFactory visitor runs just before query execution or enumeration (see provider wrappers in EntityFramework/EF Core integration projects).
2. For each `MethodCallExpression` that targets a method marked with `[ExpressionFactory]`, it tries to extract the method arguments:
   - If the argument is a `ConstantExpression` holding a runtime value, the value is used.
   - If the argument is a quoted `LambdaExpression` or a `LambdaExpression`, the visitor will attempt to coerce it into the requested `Expression<T>` type.
   - For array/new-array initializers and some method calls (e.g. `Enumerable.ToList`), the helper may compile the small expression to obtain the runtime value when safe.
   - Member expressions captured in closure containers are read when possible.
3. When all required arguments can be supplied or sensibly converted, the visitor calls the factory method (via `ExpressionFactoryInvoker`) and receives an `Expression` instance to splice into the original expression tree.
4. If an argument cannot be materialized or conversion fails, the factory call is left as-is (no unsafe evaluation).

## Implementation guidance for factories (yet so far)

- Factories should be free of side effects.
- Build lambdas using fresh `ParameterExpression` instances and avoid capturing caller parameters that cannot exist at factory invocation time.

Example implementation pattern (order-by sequence selector):

- Materialize `orderedKeys` to a list.
- Create a fresh `ParameterExpression` for the selector.
- Create a conditional chain that maps `key == value[i] ? i : previous` and return `Expression.Call(typeof(Queryable), "OrderBy", ...)`.

## Limitations, notes and safety

- **Factories MUST return a `System.Linq.Expressions.Expression`, since the returned value is to be inserted into a LINQ Expression Tree**
- The argument helper is conservative: it refuses to compile expressions that contain `ParameterExpression` nodes (to avoid compiling lambdas that depend on variables out of the current scope).
- Not all argument forms are supported. Avoid passing side-effecting or environment-dependent expressions as factory arguments.
- Factories are invoked only when arguments are materializable or sensibly convertible. If not, the factory call remains as an Expression.Call and may not be decompiled later.
- The argument helper will attempt to compile and evaluate some expression forms (arrays, simple method calls) — this is conservative and can be extended if you add new patterns; avoid side-effecting expressions in factory arguments.
- The conversion logic tries common patterns (constructing List<T> from IEnumerable<T>, converting sequences to arrays, etc.) but is not yet exhaustive.
- Tests: add unit tests that exercise both DelegateDecompiler.JIT.ExpressionFactoryVisitor and ExpressionFactoryInvoker for new scenarios.

## Tests and examples

- Tests demonstrate typical scenarios and verify that expanded factories produce the same behavior as hand-written provider-friendly expressions.
- See `src/DelegateDecompiler.EntityFramework.Tests/TestGroup91ExpressionFactoryExtensionPointFeatures/Test01OrderBySequenceLinqExtension.cs` for an end-to-end usage test.
- Also see `src/DelegateDecompiler.Tests/TestExpressionFactoryTests.cs` for unit tests that exercise the JIT visitor and factory invocation.

## Development and test workflow

Follow the repository guidelines (test-first and small incremental changes):

```bash
# restore and build
dotnet restore
dotnet build -c Debug

# run tests (example targets)
dotnet test -c Debug src/DelegateDecompiler.Tests/DelegateDecompiler.Tests.csproj
dotnet test -c Debug src/DelegateDecompiler.EntityFramework.Tests/DelegateDecompiler.EntityFramework.Tests.csproj -p:DisableGitVersionTask=true
```

- Add unit tests covering new argument patterns before changing the argument helper.
- Avoid changing tests to suit implementation bugs — fix the implementation instead.

## Troubleshooting

- If EF translation complains that an `Expression` node is used where a runtime `IEnumerable<T>` is expected, ensure the factory receives a materialized runtime sequence (e.g. pass `.ToList()` at call site or ensure the argument helper can evaluate the argument safely).
- If you see "variables out of scope" during argument compilation, that indicates the helper attempted to compile an expression containing `ParameterExpression` nodes — revise the helper logic or change how the argument is passed.




