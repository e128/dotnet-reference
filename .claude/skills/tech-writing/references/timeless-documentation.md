# Write Timeless Documentation

Describe product behavior without wording that becomes stale when time passes.

Product and reference documentation should state the behavior that applies to the documented release. It should not imply that a feature has just changed or may change later.

## Remove Time-Dependent Language

- Avoid terms such as currently, latest, new, newer, now, soon, eventually, existing, future, older, presently, and as of this writing.
- Do not assume readers know an earlier product state.
- Do not reveal or imply plans in product documentation. Use the separate future feature guidance for planned work.
- If a time dependent term is necessary, name its reference point, such as a release date or version.
- Use time-dependent language in content written to expire, such as release notes or announcements.
- Use timing in a procedure when it describes the result of an action, such as a service stopping after a command.

## Examples

Write “The command supports HTTP load balancing.” Do not call its subcommands new.

Write “The option is unsupported.” Do not write “The option is not currently supported.”

Source: [Google guidance on timeless documentation](https://developers.google.com/style/timeless-documentation).
