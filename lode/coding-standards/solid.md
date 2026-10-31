# SOLID Principles
*Updated: 2026-10-31T16:06:37Z*

## Single Responsibility Principle (SRP)
A class has one reason to change. Each class owns one concept.

## Open/Closed Principle (OCP)
Open for extension, closed for modification. Add new implementations instead of modifying existing ones.

## Liskov Substitution Principle (LSP)
A subtype must be substitutable for its base type without altering correctness.

## Interface Segregation Principle (ISP)
No client depends on a method it does not use. Prefer small, focused interfaces.

## Dependency Inversion Principle (DIP)
Depend on abstractions, not concretions. High-level modules do not depend on low-level modules.

## Balancing with YAGNI

SOLID provides guardrails. YAGNI provides brakes. Do not create an interface for a class with one implementation. Do not add an extension point for a hypothetical future requirement. Apply SOLID when the code needs the flexibility.
