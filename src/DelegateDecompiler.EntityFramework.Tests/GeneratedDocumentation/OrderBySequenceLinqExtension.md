OrderBySequenceLinqExtension
=============================

This document describes the `OrderBySequenceLinqExtension` helper used in tests to demonstrate the ExpressionFactory extension point.

Purpose
-------
- Provides an extension that allows ordering a sequence (IQueryable/IEnumerable) using an explicit sequence of keys.
- Used by tests in `TestGroup91ExpressionFactoryExtensionPointFeatures` to validate `ExpressionFactory` expansion and EF translation.

Attributes
----------
- The core factory method is marked with `[ExpressionFactory]` so it can be expanded by the ExpressionFactory visitor at query execution time.
- Public extension overloads are marked with `[Decompile]` (for the project's decompilation pipeline) and the helper `GetFirstChildIdByParent` is marked `[Computed]` to demonstrate use inside EF queries.

Public API (from tests)
-----------------------
- `IOrderedQueryable<T> OrderBySequence<T, TKey>(this IQueryable<T> source, Expression<Func<T, TKey>> keySelector, IEnumerable<TKey> orderedKeys)`
  - Builds an `OrderBy` call that orders by a selector which maps keys to their index in `orderedKeys` (unmatched keys receive `int.MaxValue`).
  - When used on an `IQueryable`, the factory expands into an expression tree that EF can translate.

- `IOrderedEnumerable<T> OrderBySequence<T, TKey>(this IEnumerable<T> source, Expression<Func<T, TKey>> keySelector, IEnumerable<TKey> orderedKeys)`
  - Same semantic as above but for `IEnumerable` sources (uses `Enumerable.OrderBy` semantics).

Expression factory
------------------
- `BuildOrderBySequenceExpression<T, TKey>(Expression source, Expression<Func<T, TKey>> keySelector, IEnumerable<TKey> orderedKeys)`
  - This private method constructs the ordering lambda that maps key values to an integer rank according to the `orderedKeys` list.
  - Implementation notes:
    - Materializes `orderedKeys` into a list (`IList<TKey> || ToList()`) so it can be indexed.
    - Builds a conditional expression chain of the form:
      `key == values[n] ? n : (key == values[n-1] ? n-1 : ... int.MaxValue)`
    - Produces either `Queryable.OrderBy` or `Queryable.OrderBy` with `AsQueryable` wrapper for `IEnumerable` sources so EF has an `IQueryable` form available.
    - The selector parameter is replaced with a fresh `ParameterExpression` so the produced lambda is self-contained.

Testing notes
-------------
- The unit test `TestBasicOrderBySequenceExtension` exercises the extension with a runtime `explicitOrder` (`List<int>`). The test compares:
  - A baseline LINQ query built with an inline conditional selector.
  - The same query built using `OrderBySequence(...)` which relies on the ExpressionFactory expansion.

Troubleshooting
---------------
- If EF translation fails with errors about `Expression` nodes being used where `IEnumerable<>` is expected, ensure the ExpressionFactory expansion yields runtime values (constants) for factory parameters that must be runtime sequences.
- Avoid compiling expression trees that capture `ParameterExpression` nodes out of scope; prefer materializing captured constants or using `AsQueryable` when converting `IEnumerable` to `IQueryable`.

Location
--------
- Test file: `src/DelegateDecompiler.EntityFramework.Tests/TestGroup91ExpressionFactoryExtensionPointFeatures/Test01OrderBySequenceLinqExtension.cs`

Revision
--------
- This documentation file was (re)generated to reflect the current implementation and to provide a concise explanation for maintainers and test-writers.
