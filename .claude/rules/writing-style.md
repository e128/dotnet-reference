# Writing Style (STE)

Write every artifact in Simplified Technical English, after ASD-STE100.

**Applies to:** docs, READMEs, PR bodies, commit bodies, error messages, code
comments, `lode/` files, `AGENTS.md`, and these rule files.

**Does not apply to:** code, identifiers, command syntax, CLI output, or chat
prose. Session style modes govern chat. The chat style rules live in
`lode/practices.md` (Chat Style).

## Words

- Use one name for one thing. Never introduce a synonym for a named thing.
- Choose short common words: use, start, make sure, before, after, show, get.
- Use American spelling.
- Ban marketing adjectives: seamless, robust, powerful, cutting-edge,
  effortless, world-class, next-generation, revolutionary.

## Verbs

- Write in the active voice. `The parser reads the file.`
- Use a verb for an action. Write `analyze the log`, not `perform an analysis`.
- Do not use an `-ing` main verb when a simple tense works.

## Sentences

- Give one instruction per sentence.
- Cap instructions at 20 words. Cap descriptive sentences at 25 words.
- Do not use contractions.
- Keep the articles: a, an, the, this, these.

## Punctuation and structure

- Never use a semicolon. Use a period.
- Cover one topic per paragraph. Cap a paragraph at 6 sentences.
- Write steps as a numbered vertical list. State the condition before the command.

## Self-lint

Check each artifact before you deliver it:

1. Any sentence over 20 words?
2. Any semicolon?
3. Any contraction?
4. Any passive voice with a known actor?
5. Any `-ing` main verb, nominalization, or phrasal verb?
6. Any thing named two ways?
