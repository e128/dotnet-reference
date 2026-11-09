<!-- repository-setup:generated sha256=057c86f92f0a0ffc7c878bcfd3c05b0969e4068e550e0b86d53afdd05820da23 -->
# AI SDLC Readier Guidance

## Design Challenge

Apply these questions before you settle a design, and again in review.

Exercise abstract thinking when considering this task.
Reduce this problem to its irreducible constraints and rebuild it from zero.
What would a principal architect immediately rewrite before even starting this?
Design the single test that breaks this design on day one.
What higher-level abstraction would make this entire problem disappear?

## State Machines

implicit state machines are not desired. do not write new ones and flag them for refactoring when found

## Module Design

Make each module deep: a simple interface hiding a lot of behavior.
If deleting a module would only push its complexity back onto its
callers, it's earning its place; if nothing would be lost, merge it
away. Put each concept in the one module that owns it, and pull
special cases, ordering rules, validation and derived values inside,
so callers never have to know them. Shrink the interface until there's
one obvious way to call it: drop any parameter, flag or option the
module can work out itself, that every caller passes the same way, or
that nobody reads, then move every caller to the new shape and delete
the old path completely.

## Dangerous Operations

Never do these three things. Each one fails quietly, and the safe alternative costs little.

- **Never push or force-push to the default branch.** A push there skips review and can rewrite shared history. Work on the setup branch, push only that branch, and let the developer open the pull request.
- **Never write outside the repository and the setup branch.** Edits to machine-level NuGet configuration, user secrets, global tool state, or another repository make a build pass on one machine and fail everywhere else. Change repository-local files such as `nuget.config` or `Directory.Build.props` on the setup branch, and tell the developer what you changed.
- **Never suppress or weaken a gate to turn a check green.** A `#pragma warning disable`, a lowered analyzer severity, or a skipped, ignored, or deleted test leaves a green check that proves nothing. Fix the finding. If you cannot, ask the developer first and list the suppression in the pull request summary.
