---
name: tech-writing
description: Write and review prose (docs, READMEs, PR bodies, commit bodies, release notes, error messages, code comments, runbooks, never code) to Google Developer Style plus house overrides and the repo writing rules. Use before writing or editing any non-code text. Triggers include "write docs", "write a README", "PR description", "make this clearer", "tighten this", "edit this prose", "tech writing", and "review my writing".
---

# tech-writing

Write any prose artifact to [Google Developer Style](https://developers.google.com/style) plus the overrides below. The topic notes in `references/` hold the full rules. Open only the note the task needs. Code, identifiers, command syntax, and chat replies are out of scope.

Order of authority:

1. `.claude/rules/writing-style.md` and `AGENTS.md`.
2. `references/house-overrides.md`.
3. Google, through the topic notes.

## Overrides That Beat Google

- **No dash as a pause.** Use a colon, a comma, parentheses, or two sentences. A `--flag` is a flag. A hyphen in a range stays. Covers the em dash, en dash, horizontal bar, minus sign, `--`, and every entity spelling.
- **Headings and titles are title-case noun phrases** with no ending period. Task headings start with a bare verb.
- **No comma-spliced noun-phrase lists** standing in for a sentence. Write a subject and a verb, or write a real list.
- **No stale counts.** Name the things, or point to the source of truth.
- **State the fact. Do not grade it.** Order and placement rank a list.
- **Docs state current state**, not edit history. A decision log, an incident record, and a dated snapshot are the exceptions.
- **Timestamps are UTC ISO 8601**, such as `2026-05-05T22:00:00Z`.
- **Repo writing rules win over Google:** no contractions, no semicolons, instructions of 20 words or fewer, descriptive sentences of 25 words or fewer, one name per thing.

## Core Rules from Google

1. Address the reader as "you" in steps. Use imperative for steps.
2. Use active voice and present tense.
3. Put the condition or goal before the instruction.
4. Put the key point first. Cover one idea per paragraph.
5. Use plain words and short sentences. Keep articles.
6. Use "must" for a requirement, "can" for an option, "we recommend" for advice.
7. Use the serial comma.
8. Write link text that stands alone. Never "click here" or a bare URL.
9. Make examples safe: reserved example domains, `user@example.com`, no real people.
10. Avoid time-anchored words, superlatives, guarantees, slang, and undefined jargon.
11. Use code font for code, bold for UI labels. Use italics only for a new term.
12. Use a Mermaid diagram when a flow or structure reads better as a picture.

## Topic Notes

Start at [index.md](references/index.md). Route by task:

| Task                                       | Open                                                                                                                                                                                          |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Any conflict with Google                   | [house-overrides.md](references/house-overrides.md)                                                                                                                                           |
| Word swaps and product names               | [word-choice.md](references/word-choice.md)                                                                                                                                                   |
| Grammar, person, tense, capitalization     | [language-mechanics.md](references/language-mechanics.md), [voice.md](references/voice.md), [sentence-structure.md](references/sentence-structure.md)                                         |
| Possessives, prepositions, reference verbs | [possessives.md](references/possessives.md), [prepositions.md](references/prepositions.md), [reference-verbs.md](references/reference-verbs.md)                                               |
| Punctuation                                | [punctuation.md](references/punctuation.md)                                                                                                                                                   |
| Lists, steps, notices                      | [lists-and-procedures.md](references/lists-and-procedures.md)                                                                                                                                 |
| Dates, numbers, units                      | [dates-numbers-units.md](references/dates-numbers-units.md)                                                                                                                                   |
| Headings, tables, links                    | [headings.md](references/headings.md), [tables.md](references/tables.md), [cross-references.md](references/cross-references.md)                                                               |
| Tone, claims, jargon, global readers       | [tone.md](references/tone.md), [excessive-claims.md](references/excessive-claims.md), [jargon.md](references/jargon.md), [translation.md](references/translation.md)                          |
| Timeless wording                           | [timeless-documentation.md](references/timeless-documentation.md)                                                                                                                             |
| Inclusive and accessible text              | [inclusive-and-accessible.md](references/inclusive-and-accessible.md)                                                                                                                         |
| Code, UI labels, placeholders              | [code-and-interfaces.md](references/code-and-interfaces.md)                                                                                                                                   |
| Images and alt text                        | [images-and-media.md](references/images-and-media.md)                                                                                                                                         |
| Filenames, example data, markup            | [names-and-markup.md](references/names-and-markup.md)                                                                                                                                         |

## Lint Before Returning Text

Run `.claude/skills/tech-writing/scripts/tech-writing-lint.sh <file>`, or pipe text through it with `-`. It flags dashes used as pauses, contractions, semicolons, sentences over 25 words, filler, time-anchored words, banned word choices, and a passive-voice heuristic. Judge each hit. The linter checks form, not content. Possessives such as "Google's" trigger a false contraction hit.
