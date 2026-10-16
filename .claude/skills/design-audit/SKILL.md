---
name: design-audit
description: >
  Reference skill: periodic design audit of a web UI against current industry guidelines. This repo ships no UI, so it holds the method for use on consumer repos. Checks the diff since the
  last audit, then WCAG 2.2 A/AA conformance, Core Web Vitals, responsive usability, Baseline CSS
  features, color, typography, layout, interaction, and pinned CDN dependencies. Writes a dated
  report and an unapplied patch. Does not auto-invoke.
  Triggers on: design audit, UI audit, WCAG check, accessibility audit, CSS review,
  responsive audit, Core Web Vitals check, Baseline check, audit the frontend.
---

# Design Audit

Research-and-report skill. It never edits the UI source in the working tree. It writes two files
under `plans/` and stops. Manual invocation only.

**Scope.** Reference skill. This repository ships no UI, so it holds the audit method and cites
the standards it rests on (W3C WCAG, WAI-ARIA APG, ISO/IEC 40500, ETSI EN 301 549, web.dev Baseline
and Core Web Vitals, MDN). Run it against a consumer repo or sample app covering any web UI: static
HTML, Razor or Blazor output, or a SPA. State the target path in the report header.

**Why it exists.** Two kinds of finding come out of a run. The field moves past a team decision
rarely. The code drifts from a team decision often, because edits are made without rereading the
decision record. Steps 1 and 2 target the second kind. Step 3 targets the first.

## Step 1: Load Decisions and the Last Audit

Read these before anything else:

1. The target's contributor and design docs: `AGENTS.md`, `CLAUDE.md`, `README.md`, and any
   design-decision file (`NOTES.md`, `docs/design*`, `docs/adr/`, `lode/`). Treat each recorded
   decision and prohibition as the **decision set**. If none exists, say so in the report and treat
   every finding as new ground.
2. The newest `plans/design-audit-*.md`, if one exists. Read its header for the commit SHA it audited.
3. `plans/design-audit-declined.md`, if it exists. It is a table: `| Date | Topic | Finding | Reason |`.
   This skill appends a row only when the maintainer declines a finding in conversation. A run never
   writes a row unprompted and never removes one.

Reread the files each run. Do not rely on recall from an earlier turn.

How the previous report changes this run:

- **Window.** Anchor on a commit, not a date (Step 2).
- **Repeats.** A finding already reported with nothing new behind it gets one line:
  `Repeat, unchanged since YYYY-MM-DD`. Do not re-argue it and do not put it in the patch.
- **Declines.** A declined finding is out of scope unless a source dated after the decline
  supports it. Then it is a challenge (Step 5) and the report quotes the ledger row.
- **Wrong claims.** Recheck any claim in the last report that names a file, selector, or owner and
  that this run's delta touches. Correct it and name the report you correct.

## Step 2: Audit the Delta First

```bash
PREV=<SHA from the previous report header>
git log --oneline "$PREV..HEAD"
git diff --stat "$PREV..HEAD"
git diff "$PREV..HEAD" -- <stylesheets, templates, components, scripts>
```

With no previous report, use the last ten commits that touched UI files. State the range and the
commit count in the report header. An empty delta takes one line. Spend that effort on Step 4.

For each added or changed UI line, ask four questions:

1. **Is it documented?** A new component with no decision-record entry is a finding.
2. **Does it obey the repo's written rules?** Check ungated rules first (Step 4).
3. **Does it obey a recorded decision?** This is the high-yield question.
4. **Does a fixture, story, or page render it?** A styled selector with no rendered instance is
   untested.

Classify each result with the Step 3 labels, plus **Violation of a decision**: the code broke a
decision that still stands. A violation goes in the patch. It is never a challenge.

## Step 3: Research Topics

Run one topic at a time. Each topic maps to the decision-record sections that cover it. Build the
map from the target's own docs, and list unmapped sections in the report as out of scope.

| # | Topic | Covers | Guideline sources |
| --- | --- | --- | --- |
| 1 | Color and contrast | Tokens, modes, dark theme, forced colors, print | WCAG 1.4.1, 1.4.3, 1.4.11. WAI contrast guidance |
| 2 | Typography | Type scale, fonts, `text-wrap`, line length, text spacing | WCAG 1.4.4, 1.4.12. MDN, web.dev |
| 3 | Layout and spacing | Container queries, `:has()`, intrinsic sizing, breakpoints, RTL | WCAG 1.3.2, 1.4.10. MDN |
| 4 | Accessibility | Roles, names, focus, keyboard, status messages, ARIA patterns | WCAG 2.2. WAI-ARIA APG |
| 5 | Interaction and motion | `prefers-reduced-motion`, hover and press states, transitions | WCAG 2.2.2, 2.3.3, 1.4.13. MDN |
| 6 | Pinned dependencies | CDN and package versions, fixes worth taking, provenance | npm registry, release notes |
| 7 | Performance and stability | Core Web Vitals, render blocking, layout shift, CSS coverage | web.dev |

For each topic:

1. **Search first.** Use a specific, current query. Prefer W3C, WAI, MDN, web.dev, caniuse, and
   browser vendor blogs over aggregators. Two to four sources per topic suffice. Record title,
   URL, and access date for each source. List every query under `### Searched`, even a query that
   returned nothing. Scale effort to the window: after a short range, one query per topic suffices.
2. **Date every browser-support claim.** Cite the Baseline status and the date the feature reached
   Baseline (web.dev Baseline, MDN, or caniuse). "Supported now" is not evidence.
3. **Compare against the real code**, not against memory of it.
4. **Classify each finding:**
   - **New ground.** No recorded decision. Propose freely.
   - **Reinforces a decision.** Current practice still agrees. Say so.
   - **Repeat.** One line.
   - **Declined.** In the ledger with no later source. Drop it silently.
   - **Violation of a decision.** Goes in the patch.
   - **Challenges a decision.** Current practice now disagrees with a recorded decision. See Step 5.

### Render, Do Not Reason About the Cascade

A layout or contrast claim is not verified until a browser draws it. Any finding or fix that
touches a new or changed component gets a probe render. Build a throwaway page in the scratchpad
(`.claude/tmp/`) with the real stylesheet and real markup, then measure it:

- **1280 px** for resting geometry.
- **320 px** for reflow and target size.
- **`dir="rtl"`** for physical-side regressions. Read computed `border-*-width`, not the declaration.
- **Print emulation** for print overrides. Read the computed value on the element the rule targets.

Measure again after the patch. Put both numbers in the report. A media query adds no specificity,
so an override placed above the rule it must beat loses on source order. Render to catch this.

### Responsive Usability Sweep

Run on every audit. Test each key page at every combination of:

- Widths: 320, 375, 768, 1024, 1280 CSS pixels.
- Heights: 568, 768, 900 CSS pixels.

Record page, viewport, `document.documentElement.scrollWidth`, `clientWidth`, and any intentional
overflow. Separate page-level horizontal scroll from scrolling inside a documented hatch such as a
table, code block, or diagram. Check flex and grid order against DOM order. Check heading order and
visual hierarchy at each size. Test every loading, error, and empty state the UI implements. If the
UI has none, say so. Do not invent states.

Measure each interactive target from its rendered rectangle. Report targets under 44 x 44 CSS pixels
as a usability gap. Keep that separate from WCAG 2.5.8 (24 x 24 CSS pixel minimum, with exceptions).
The 44 x 44 check never changes the 2.5.8 result. Report under `## Responsive Usability Sweep`.

### Topic 4: WCAG Conformance Sweep

This sweep asks a harder question than the research topics: does the UI fail a success criterion
right now, regardless of any recorded decision?

**Find the current standards first.** Check the W3C WCAG page and the W3C TR index for the latest
Recommendation and its revision date. Check WCAG 3.0 separately and treat it as draft work only.
Never report a draft outcome as a conformance criterion. Check the ISO/IEC 40500 edition and the
ETSI EN 301 549 version and Official Journal status. Cite sources and the access date. Never claim
legal compliance (EU Accessibility Act, Section 508, ADA) from a standards listing.

**Run the criteria below**, plus any criterion the current Recommendation adds or changes. Mark a
criterion that needs absent content `Not applicable` with a reason. Never omit a row.

| Criterion | Level | Check against |
| --- | --- | --- |
| 1.1.1 Non-text Content | A | `alt` on images, accessible names on SVG and diagrams, decorative content hidden |
| 1.3.1 Info and Relationships | A | Semantic tables, heading hierarchy, list and form semantics |
| 1.3.2 Meaningful Sequence | A | DOM order against visual order (flex or grid reorder, multicolumn) |
| 1.4.1 Use of Color | A | Color is never the only cue (status, links, errors) |
| 2.1.1 Keyboard | A | Every control and scroll region is reachable and operable |
| 2.1.2 No Keyboard Trap | A | Dialogs and widgets let focus move away |
| 2.4.2 Page Titled | A | `<title>` present and distinct per page |
| 2.4.3 Focus Order | A | Tab order preserves meaning and operability |
| 2.4.4 Link Purpose (In Context) | A | No bare "click here". Icon links have names |
| 2.5.3 Label in Name | A | Visible label text is part of the accessible name |
| 3.1.1 Language of Page | A | `lang` on `<html>` |
| 3.2.1 On Focus | A | Focus never triggers a context change |
| 3.2.2 On Input | A | Input never navigates or submits unexpectedly |
| 3.2.6 Consistent Help | A | Repeated help keeps its relative order |
| 3.3.7 Redundant Entry | A | Earlier entries are reusable in the same process |
| 4.1.2 Name, Role, Value | A | Custom widgets expose name, role, and state |
| 1.4.3 Contrast (Minimum) | AA | Measure text against every ground. Cross-reference Topic 1 |
| 1.4.4 Resize Text | AA | 200% zoom without loss of content |
| 1.4.10 Reflow | AA | No two-dimensional scroll at 320 CSS px outside opt-in hatches. Measure it |
| 1.4.11 Non-text Contrast | AA | Focus rings, borders, and control outlines. Check forced-colors rules |
| 1.4.12 Text Spacing | AA | Layout survives user overrides of line height, letter spacing, margins |
| 1.4.13 Content on Hover or Focus | AA | Hover content is dismissable, hoverable, persistent |
| 2.4.6 Headings and Labels | AA | Headings and labels describe their purpose |
| 2.4.7 Focus Visible | AA | Visible focus indicator in every mode |
| 2.4.11 Focus Not Obscured (Minimum) | AA | Sticky headers and overlays never fully hide focus |
| 2.5.7 Dragging Movements | AA | A single-pointer alternative exists when dragging exists |
| 2.5.8 Target Size (Minimum) | AA | 24 x 24 CSS px or an exception. Measure from a render |
| 3.2.3 Consistent Navigation | AA | Repeated navigation keeps its order |
| 3.2.4 Consistent Identification | AA | Same function, same identification |
| 3.3.8 Accessible Authentication (Minimum) | AA | No cognitive function test unless an exception applies |
| 4.1.3 Status Messages | AA | Live regions announce results and errors |

For 2.5.8, check each exception: inline, spacing (a 24 px circle on each undersized target touches no
other target or circle), equivalent control, user-agent control, and essential presentation.

**Relevant AAA criteria, in a separate table:** 2.4.12 Focus Not Obscured (Enhanced), 2.4.13 Focus
Appearance, 2.5.5 Target Size (Enhanced), 3.3.9 Accessible Authentication (Enhanced). Never report an
AAA gap as an AA failure.

**Out of scope, state once:** 1.2.x when no audio or video exists, 2.2.x without timers, 2.3.x without
flashing content, 2.4.1 and 2.4.5 for a single-document UI, 2.5.1, 2.5.2, 2.5.4 without custom
gestures or motion input, 3.1.2 for single-language content, and 3.3.1 through 3.3.6 without forms.
Include any of these the target actually has.

**Classify every row:**

- **Pass.** One line of evidence.
- **Accepted gap.** A recorded decision knowingly accepts it. Quote the passage and confirm it still
  describes current behavior.
- **Violation.** Fails and no decision accepts it. State the failure concretely (selector, missing
  attribute, reproducible interaction). Put a mechanical fix in the patch. A fix that reverses a
  decision becomes a Step 5 challenge. A design judgment stays out of the patch, with the reason.
- **Not applicable.** Name the absent feature.

Re-evaluate every row against whatever Step 2 found new. A component never swept has never passed.
Report under `## WCAG Conformance Sweep` with columns (Criterion, Level, Status, Evidence).

### Manual Accessibility Checks

Test keyboard use by hand in each appearance mode. Use Tab and Shift+Tab to visit every interactive
element. Use Enter, Space, and Escape where the control supports them. Confirm a visible focus
indicator and reachable critical actions. Never send local or private pages to an external scanner.
Automated results supplement manual checks and never prove conformance. Report under
`## Accessibility and Keyboard Checks`.

### Topic 6: Pinned Dependencies

Read pins from the files, not from memory. For each, report the current release
(`npm view <pkg> version time.modified`), whether anything between the two is a rendering or security
fix, and whether the bump is worth taking. Never propose a range, `latest`, or an untagged URL: pin an
exact version.

Check provenance, not only freshness. A CSS `@font-face` URL and a bare ESM `import` of a remote URL
have no `integrity` hook, so do not propose an `integrity=` attribute for them. A `<script>` or
`<link>` tag can carry SRI, so check that it does. Check whether the npm package has a provenance
attestation (`npm view <pkg> dist.attestations`). Read the decision record before proposing
self-hosting. If it was weighed, mark the finding `Repeat`.

### Topic 7: Performance and Stability

Verify the current Core Web Vitals thresholds from web.dev on each run and cite the access date. At
the time of writing, good targets are LCP at or below 2.5 s, INP at or below 200 ms, and CLS at or
below 0.1. Do not assume they stay current.

- Label lab measurements as lab data. Record tool, browser version, page, viewport, emulation,
  throttling, and results.
- Report field data (CrUX or RUM) separately for mobile and desktop at the 75th percentile. Report
  INP as unavailable unless valid field data exists. Total Blocking Time is not INP.
- Never send a local or private page to PageSpeed Insights. Use it only for a public URL, with the
  maintainer's authorization.
- Check render-blocking resources. Check images and embeds for reserved width and height or an
  `aspect-ratio`. Report ad layout shift as not applicable without an ad slot.
- Run DevTools Coverage across implemented states. Report unused CSS as a candidate only. Coverage
  never proves that no consumer uses a rule. Never remove CSS on coverage alone.

Report under `## Topic 7: Performance and Stability`.

## Standards Status

Recheck on every audit. Use the audit date as the status date. Never reuse a prior status.

| Area | Source to check | Report |
| --- | --- | --- |
| WCAG Recommendation | W3C WCAG page, W3C TR index | Version, status, dates, changed criteria |
| WCAG 3.0 | W3C TR draft and publication history | Latest draft type and date, explicit draft status |
| ISO/IEC 40500 | ISO catalog | Current edition and status |
| EN 301 549 | ETSI records, EU Official Journal | Version, date, Official Journal citation |
| Baseline | web.dev Baseline, MDN | Definition checked, status date |

Report under `## Standards Status` with `### Searched` (query, source title, URL, access date). State
that the audit maps technical criteria only and claims no legal compliance.

## Modern CSS Baseline Review

Check each feature against current MDN or web.dev data. Report one row per feature under
`## Modern CSS Baseline Review`: Baseline tier, Newly available date, current use in the code, and the
fallback or the reason not to adopt it. Use `Not Baseline` when no status exists. A Newly available
feature needs an `@supports` fallback unless consumer needs make it unnecessary. State why. `@supports`
tests syntax support, not interoperability. Never propose a feature only because it crossed a Baseline
threshold.

Features: `@container`, `:has()`, subgrid, `@layer`, CSS nesting, `@scope`, `oklch()` and
`color-mix()`, `clamp()`, logical properties, `aspect-ratio`, Flexbox `gap`, `dvh`/`svh`/`lvh`,
scroll-driven animation, View Transitions, `@starting-style`, Grid Lanes (masonry), `sibling-index()`,
anchor positioning, `@property`.

Also evaluate mobile-first authoring as a practice. It never forces a reversal of a settled layout.

## Step 4: Ungated-Rules Sweep

A prose rule with no automated gate stays alive only if an audit checks it. Run each row. The commands
are starting points. Read the hits.

| Rule | Check |
| --- | --- |
| Rules in the target's contributor docs that no CI gate enforces (comments, naming, token use) | List each rule, then search the code for a violation |
| Every `var(--x)` resolves to a declared token or has a fallback | Set difference of references against declarations |
| Logical sides, not physical (`margin-inline`, not `margin-left`) | `rg -n 'border-(left\|right)\|padding-(left\|right)\|margin-(left\|right)'` |
| Hover rules sit inside `@media (hover: hover)` | List `:hover` rules and confirm each is inside the block |
| Decorative generated content has an accessible-name twin | Check each string-valued `::before` and `::after` |
| Styled selectors have a rendered instance, or a documented reason | Compare selectors against fixtures or stories |
| Exact dependency pins | No `^`, `~`, or `latest` in a CDN URL or package reference |
| No hardcoded URL, secret, or CORS origin in UI code | Search the source. See `.claude/rules/security.md` |

Status per row: `Pass`, `Violation`, or `Not reachable this run` (say why). Report under
`## Ungated-Rules Sweep`.

A row that is cheap to mechanize may become a gate in the patch, with two limits. A gate that cannot
reach its subject does not get written: prefer an honest decision-record paragraph. A gate that would
fail on `HEAD` goes in the patch together with the fix it demands, or not at all.

## Step 5: Challenges Need Sign-Off

A recorded decision may be reopened, but never quietly. Each challenge must:

- Quote the exact passage with its section heading.
- State what changed since it was written (spec landing, Baseline date, new measurement), with a
  cited source and date.
- Sit under `## Challenges a Settled Decision`, apart from ordinary findings.
- Carry the line: `Requires maintainer sign-off before this touches the decision record or the code.`

A challenge never enters the patch, in this run or any run. A challenge says the decision may be wrong
now. A violation says the code broke a decision that still stands. A violation always goes in the patch.

## Step 6: Write the Artifacts

Get the date once (`date +%F`) and reuse it. A second run on the same day overwrites the first.

- `plans/design-audit-YYYY-MM-DD.md`: the report. Include a header (target path, commit range and
  count, previous report read), the Step 2 delta findings, the seven topics with `### Searched`,
  `## Standards Status`, `## Modern CSS Baseline Review`, `## Responsive Usability Sweep`,
  `## Accessibility and Keyboard Checks`, `## WCAG Conformance Sweep` with a separate AAA table,
  `## Ungated-Rules Sweep`, any challenges, one line naming the out-of-scope sections, and the
  verification verdict.
- `plans/design-audit-YYYY-MM-DD.patch`: a unified diff against `HEAD` for non-challenging findings
  only. A draft to review, never applied automatically. With no such findings, write no patch and say
  `no patch: nothing to propose` in the report.

The patch obeys the target's own rules (comments, tokens, generated files). It touches source files
only, never generated output. Author it in a throwaway worktree so the real tree stays clean. Create
`plans/` if missing. Touch no other file.

## Step 7: Verify the Patch

Skip when no patch exists. Apply the patch to a detached worktree at `HEAD`, regenerate any generated
files inside it, and run the target's own gate (for example `scripts/check.sh`). Then rerun the Step 3
probe render against the patched code and record the before and after numbers.

```bash
D=$(date +%F)
git worktree add --detach .claude/tmp/design-audit-verify HEAD
git -C .claude/tmp/design-audit-verify apply "$PWD/plans/design-audit-$D.patch"
# regenerate and run the target's gate inside the worktree
git worktree remove --force .claude/tmp/design-audit-verify
```

Remove the worktree even when the gate fails. Scan the added patch lines for a banned construct the
target's gate cannot detect (for example a comment in a no-comment file). Record exactly one verdict:

- `Verified: patch applies to HEAD and the gate passes after regeneration.`
- `FAILED: <shortest decisive output line>.` Fix and reverify, or move the finding to the report body.
- `Unverified: <reason>.` Use only for a missing local dependency such as a browser. Never write
  `Verified` for a gate that did not conclude.

## Step 8: Report Back, Then Stop

State where the files landed, the verdict, and one line of counts: total findings, violations, new,
repeats, challenges, and unavailable evidence checks. Stop. Applying the patch, editing the decision
record, appending to the declined ledger, bumping a dependency, or releasing is a separate ask.

## Rules With No Exception

- Never edit UI source or generated files in the working tree. Change code only in a throwaway worktree.
- A challenged decision never enters the patch. A violated decision always does.
- Never write `Verified` for a gate that did not conclude.
- Never write to the declined ledger unprompted. Never remove a row.
- Never claim a browser feature is usable without a Baseline date.
- Never claim a layout, contrast, or target-size fix works without a render that shows it.
- Never claim legal compliance. Map technical criteria only.
