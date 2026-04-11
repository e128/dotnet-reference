# SOLID Principles
*Updated: 2026-04-11T14:11:27Z*

## Single Responsibility Principle (SRP)
A class should have only one reason to change. Each class owns one concept.

## Open/Closed Principle (OCP)
Open for extension, closed for modification. Prefer adding new implementations over modifying existing ones.

## Liskov Substitution Principle (LSP)
Subtypes must be substitutable for their base types without altering correctness.

## Interface Segregation Principle (ISP)
No client should be forced to depend on methods it does not use. Prefer small, focused interfaces.

## Dependency Inversion Principle (DIP)
Depend on abstractions, not concretions. High-level modules should not depend on low-level modules.

## Balancing with YAGNI

SOLID provides guardrails; YAGNI provides brakes. Don't create interfaces for classes with one implementation. Don't add extension points for hypothetical future requirements. Apply SOLID when the code naturally needs the flexibility, not preemptively.
