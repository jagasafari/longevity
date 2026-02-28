# Copilot Instructions

The user uses voice-to-text input. English is not their native language.

When responding:
- First, silently correct any grammar, spelling, or transcription errors
- Infer correct meaning from context when words are misheard
- Then answer the technical question clearly and concisely

## Code style

### General (all languages)
- Pure functional by default: no mutable state, no side effects in business logic
- Separate pure functions from impure IO (thin impure shell, thick pure core)
- Prefer expressions over statements
- Prefer immutable data structures (records, discriminated unions)
- Keep functions small, composable, and testable in isolation
- Minimize lines of code — concise > verbose
- No code comments — code should be self-documenting via clear naming
- Keep script usage comments (e.g. `# Usage: pwsh script.ps1 [-Flag]`)
- Inject side effects (randomness, IO, time) as function parameters
- Pass only what a function needs — never forward an entire record when a subset suffices
- Make illegal states unrepresentable: encode domain rules in types, not runtime checks
  - Use discriminated unions / sum types instead of booleans or string flags
  - Use `option`/`Result` instead of nulls or exceptions
  - Prefer narrowing inputs to the smallest valid type over defensive validation

### PowerShell
- Keep lines to a maximum of 64 characters
- Extract variables instead of using long inline expressions
- Prefer functional style (inspired by F#/Haskell):
  - Avoid mutable variables; derive state from data
  - Use pipelines (`|`) instead of loops where possible
  - Collect results into arrays, then reduce once at the end
  - Prefer expressions over statements (e.g. `$x = if (...) { a } else { b }`)
  - No side-effectful mutation inside conditionals
