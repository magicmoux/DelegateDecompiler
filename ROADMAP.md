# Additional Features RoadMap

## Overview
This document outlines the roadmap for additional features to be implemented in the project. 

- The features are categorized based on their priority and estimated timeline for completion.
- Features must be evaluated for feasibility and impact before being added to the development cycle.
- Solutions' proposed plans and implementation must be redacted in English and must follow the established coding standards, best practices and Copilot's instruction set for this repo.
- Proposals muste be referenced in the Roadmap using markdown syntax `[Proposed Plan](...)`

## Priority Features
- High : XOR simplificable patterns decompiled optimisation
- Medium : Expression tree comparer
- Low : Complementary processing IoT on DecompiledQueryables

### XOR simplificable patterns decompiled optimisation
This feature aims to optimize the decompilation process for XOR patterns, like DefaultOrMatching patterns (i.e. `!collection.Any() ^ collection.Contains(element)`) or similar variations where both terms are mutually exclusive.
If both members of the XOR expression are mutually exclusive, the XOR is equivalent to a simple OR pattern and can be optimized accordingly, leading to eviction of redundant exclusivity checks in the ORMs' translated queries.

Problem: Upon compilation of an OR operation where the members are mutually exclusive, the compiler might destructure the expression tree into multiple If-Then-Else instructions that may not be reasonably reversed by DelegateDecompiler into an optimal simple OR. 
On the other hand while XOR pattern will be preserved, the query Optimizer is not able to detect the members as naturally exclusive and to simplify it into a simple OR to avoid exclusivity checks by the ORM translator.

Goal: find a way to analyse XOR expressions and detect whether members are mutually exclusive and optimize the decompilation process to produce the equivalent OR expression.

[Proposed plan](./Xor-optimisation-Implementation-Plan.md)

### Complementary processing IoT on DecompiledQueryables
This feature focuses on defining way to decouple the decompilation process from custom processing chains that should occur between the decompilation of a query and the translation to the ORM's query language.

Problem: in some cases, users may want to perform custom processing on the decompiled expression tree before it is translated to the ORM's query language. This could include optimizations, transformations, or other manipulations that are specific to their application's needs.

Goal: define a way to allow users to plug in custom processing chains that can be executed after the decompilation process and before the translation to the ORM's query language. 
This could range from simple instructions chaining to defining an interface for custom processors, and providing a mechanism for users to register their processors with the decompilation pipeline.

Basic proposal: add a simple `Action<>` parameter to the `Decompile` method that allows users to specify a custom processing chain action that should be executed on the decompiled expression tree before it is translated to the ORM's query language.

[Proposed plan](./PostDecompile-ProcessingChain-Implementation-Plan.md)