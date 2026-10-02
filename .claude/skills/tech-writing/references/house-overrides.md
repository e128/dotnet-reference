# House Overrides to Google Style

Google Developer Style is the base standard for prose in this repository. The rules below win where they differ from it. This repository's writing rule (`.claude/rules/writing-style.md`) wins over both.

## Where House Rules Win

| Topic                       | Google Says                                  | House Rule                                                                                                                      |
| --------------------------- | -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Dashes                      | An em dash is allowed with no spaces         | **Never use a dash as a pause.** Use a colon, a comma, parentheses, or two sentences. A hyphen in a range or compound stays.    |
| Headings                    | Sentence case                                | Title case noun phrase, no ending period. Task headings start with a bare verb.                                                 |
| Comma-spliced noun phrases  | No rule                                      | Write a sentence with a subject and a verb, or write a real list. A comma never replaces the verb.                              |
| Counts and ratios           | No rule                                      | Name the things. A count that goes stale in silence is deleted, not corrected.                                                  |
| Grading a fact              | Avoid excessive claims                       | State the fact. Order and placement rank a list, not an adjective.                                                              |
| Edit history                | Timeless documentation                       | A document states current state. Decision logs, incident records and dated snapshots are the only exceptions.               |
| Phone number hyphens        | Use nonbreaking hyphen entities              | Use plain hyphens, because the dash scan flags the entity.                                                                      |
| Timestamps                  | Format varies                                | UTC ISO 8601, such as `2026-05-05T22:00:00Z`.                                                                                   |

## Where Repo Rules Win Over Google

| Topic            | Google Says                                  | Repo Rule                                                                 |
| ---------------- | -------------------------------------------- | ------------------------------------------------------------------------- |
| Contractions     | Allowed                                      | Never use a contraction in an artifact.                                   |
| Semicolons       | Allowed for closely related clauses          | Never use a semicolon. Use a period.                                      |
| Sentence length  | No hard cap, aim for fewer than 26 words     | Cap an instruction at 20 words. Cap a descriptive sentence at 25 words.   |
| Paragraphs       | Five or six sentences at most                | Cover one topic per paragraph. Cap it at 6 sentences.                     |
| Vocabulary       | Plain words, no controlled list              | Use one name per thing. Pick short common words. Use American spelling.   |
| Reader           | Address the reader as "you"                  | Keep "you" in steps. Do not use a first-person plural in an artifact.     |

Chat replies are out of scope. `lode/practices.md` (Chat Style) governs chat.

## Where Google Wins

- Use "you" for the reader. Use "must" for a requirement, "can" for an option, and "we recommend" for advice.
- Put the condition before the instruction.
- Keep the serial comma, active voice and present tense.
- Plain words beat long words. Exact technical terms stay.

## Order of Authority

1. `.claude/rules/writing-style.md` and the active repository instructions (`AGENTS.md`, `CLAUDE.md`).
2. The house rules in this note.
3. Google Developer Style, through the topic notes in the index.

Source: [Google philosophy of the guide](https://developers.google.com/style/philosophy). The guide records Google's own decisions, so a team can set its own where it must.
