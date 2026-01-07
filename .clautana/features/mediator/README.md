# Feature: mediator

**Status**: Exploring
**Created**: 2026-01-05

## Problem Statement

Modern .NET applications often suffer from tight coupling between application layers, making code difficult to test, maintain, and evolve. Common problems include:

**Coupling Issues:**
- Controllers directly instantiate and call business logic services
- Business logic is scattered across multiple layers without clear boundaries
- Cross-cutting concerns (logging, validation, caching) are mixed with business logic
- Testing requires complex mocking of multiple dependencies

**CQRS Implementation Challenges:**
- No clear separation between read and write operations
- Query optimization is difficult when mixed with command handling
- Different scaling requirements for reads vs writes cannot be addressed independently
- Business events are tightly coupled to the operations that trigger them

**Code Organization Problems:**
- Request/response patterns are inconsistent across the application
- Error handling is duplicated and inconsistent
- Pipeline behaviors (validation, logging, etc.) are manually wired in each endpoint
- No standardized way to handle cross-cutting concerns

**Migration Pain Points:**
- Existing applications using MediatR face breaking changes during upgrades
- Heavy dependency on external libraries increases security and maintenance burden
- Performance overhead from unnecessary abstractions and allocations

The mediator pattern addresses these issues by providing a lightweight, decoupled communication mechanism that centralizes request handling while maintaining separation of concerns and enabling consistent cross-cutting behavior application.

## Constraints

### Technical Constraints

**Performance Requirements:**
- Must maintain sub-millisecond overhead for handler resolution and invocation
- Memory allocation should be minimized to avoid GC pressure in high-throughput scenarios
- Handler caching must be thread-safe without introducing lock contention
- Pipeline construction should not significantly impact request latency

**Framework Compatibility:**
- Must target .NET 8.0 and .NET 9.0 to support modern and current LTS versions
- Cannot depend on framework-specific features that break multi-targeting
- Must work with both ASP.NET Core and console applications
- Assembly scanning must handle complex deployment scenarios (single-file, trimmed, AOT)

**Dependency Management:**
- Core library must have zero external dependencies beyond Microsoft.Extensions.DependencyInjection.Abstractions
- Cannot introduce breaking changes to public API surface without major version bump
- Must maintain source and binary compatibility within major version

**Reflection and AOT Constraints:**
- Dynamic handler type construction must work with .NET Native AOT compilation
- Generic type caching strategies must be AOT-compatible
- Assembly scanning must support trimming-friendly registration patterns
- MethodInfo.Invoke calls must handle TargetInvocationException unwrapping correctly

### Organizational Constraints

**Migration Compatibility:**
- Must provide clear migration path from MediatR 12.5 without requiring architectural changes
- Breaking changes should be limited to method signatures and naming conventions
- Existing handler patterns should translate directly with minimal code changes

**Testing and Quality:**
- Must maintain >90% code coverage across all components
- Integration tests must validate behavior in realistic dependency injection scenarios
- Performance benchmarks must demonstrate improvement over MediatR in common scenarios
- Documentation must include migration guides and architectural decision rationale

**Packaging and Distribution:**
- NuGet package size must remain minimal (< 50KB for core library)
- Separate packages for core functionality vs. DI integration to allow selective dependency
- Must support both PackageReference and packages.config scenarios
- Symbol packages must be published for debugging support

### Domain-Specific Constraints

**CQRS Pattern Adherence:**
- Commands must enforce fire-and-forget semantics (no return values except Task)
- Queries must be side-effect free and cacheable by default
- Notifications must support multiple handlers with independent failure handling
- Request/response types must be immutable and serialization-friendly

**Exception Handling Requirements:**
- Must distinguish between business exceptions and technical exceptions
- Exception handlers must support both fallback responses and side-effect actions
- Stack traces must be preserved through the mediation pipeline
- Cancellation tokens must be properly propagated through all async operations

**Security Considerations:**
- Handler resolution must validate type safety to prevent injection attacks
- Generic type constraints must prevent unauthorized handler invocation
- Pipeline behaviors must not leak sensitive information in exception messages
- Assembly scanning must respect security policies in restricted environments

## Investigations

| Investigation | Status | Summary |
|--------------|--------|---------|

## Decision

*No ADR yet - investigations in progress*
