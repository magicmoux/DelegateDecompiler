# Additional Features RoadMap

## Overview
This document outlines the roadmap for additional features to be implemented in the project. 

- The features are categorized based on estimated timeline for completion.
- Features must be evaluated for feasibility and impact before being added to the development cycle.
- Solutions' proposed plans and implementation must be redacted in English and must follow the established coding standards, best practices and Copilot's instruction set for this repo.
- Proposals must include the roadmap feature content for initial context 
- Proposals must be referenced in the Roadmap using markdown syntax `[Proposed Plan](...)`

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
