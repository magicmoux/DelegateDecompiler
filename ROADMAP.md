# Additional Features RoadMap

## Overview
This document outlines the roadmap for additional features to be implemented in the project. 

- The features are categorized based on estimated timeline for completion.
- Features must be evaluated for feasibility and impact before being added to the development cycle.
- Solutions' proposed plans and implementation must be redacted in English and must follow the established coding standards, best practices and Copilot's instruction set for this repo.
- Proposals muste be referenced in the Roadmap using markdown syntax `[Proposed Plan](...)`

## Proposed features
- XOR simplificable patterns decompiled optimisation
- Complementary processing IoT on DecompiledQueryables

### XOR simplificable patterns decompiled optimisation
This feature aims to optimize the decompilation process for XOR patterns, like DefaultOrMatching patterns (i.e. `!collection.Any() ^ collection.Contains(element)`) or similar variations where both terms are mutually exclusive.
If both members of the XOR expression are mutually exclusive, the XOR is equivalent to a simple OR pattern and can be optimized accordingly, leading to eviction of redundant exclusivity checks in the ORMs' translated queries.

**Problem:** Upon compilation of an OR operation where the members are mutually exclusive, the compiler might destructure the expression tree into multiple If-Then-Else instructions that may not be reasonably reversed by DelegateDecompiler into an optimal simple OR. 
On the other hand while XOR pattern may be preserved, the query optimizer is not able to detect the members as naturally exclusive and to simplify it into a simple OR to avoid exclusivity checks by the ORM translator.

**Goal:** find a way to analyse XOR expressions and detect whether members are mutually exclusive and optimize the decompilation process to produce the equivalent OR expression.

[Proposed plan](./Xor-optimisation-Implementation-Plan.md)

### Complementary processing IoT on DecompiledQueryables
This feature focuses on defining a way to decouple the decompilation process from custom processing chains that need to exploit a DecompiledQueryable optimized expression for specific application needs.

**Problem:** in some cases, users may need to perform custom processing on a decompiled query upon execution in an automatable way. This may include analysis, logging, telemetry, query policy checks, or other domain-specific operations.

As for why this feature might be best addressed within DelegateDecompiler, the extensions need to be invoked during query execution within the `GetEnumerator` or `GetAsyncEnumerator` methods of `DecompiledQueryable<T>` or `Execute` methods of `DecompiledQueryProvider`.

**Goal:** define a way to allow users to plug in custom processing chains that can be invoked automatically through native DelegateDecompiler methods between decompilation and ORM-specific translation/execution phases.
Solutions may range from fluent instructions chaining, simple event handlers, or dedicated processor interfaces with registration mechanisms.

[Proposed plan](./IoT-DecompiledQueryables-Implementation-Plan.md)
