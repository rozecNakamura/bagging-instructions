# Repository Guidelines

All commentary returned by the agent should be in Japanese.

First, review the user instructions:
<instructions>
{{instructions}}
<!-- This template variable will be automatically replaced with user input prompt -->
</instructions>

Based on these instructions, proceed with the following process:

---

## 1. Instruction Analysis and Planning

<task_analysis>
- Summarize the main task concisely
- **Always check specified rule directories/files to follow**
- Identify key requirements and constraints
- List potential challenges
- Detail specific steps for task execution
- Determine optimal execution order

### Preventing Duplicate Implementation
Before implementation, verify:
- Existence of similar features
- Functions/components with same or similar names
- Duplicate API endpoints
- Identifiable common processes

Invest adequate time in this section as it guides the entire subsequent process. Ensure comprehensive and detailed analysis.
</task_analysis>

---

## 2. Task Execution

- Execute identified steps sequentially
- Report progress concisely after each step completion
- During implementation, ensure:
  - Adherence to proper directory structure
  - Consistent naming conventions
  - Appropriate placement of common processes

---

## 3. Quality Control and Issue Resolution

- Verify execution results promptly
- Handle errors/inconsistencies with:
  a. Problem isolation and root cause analysis (log analysis, debug info)
  b. Create and implement solutions
  c. Verify post-fix operation
  d. Check and analyze debug logs

- Record verification results as:
  a. Verification items and expected results
  b. Actual results and discrepancies
  c. Required actions (if applicable)

---

## 4. Final Verification

- Evaluate all deliverables upon task completion
- Confirm alignment with original instructions, adjust as needed
- Final check for implementation duplicates

---

## 5. Result Reporting

Report final results in this format:

```markdown
# Execution Report

## Summary
[Brief overall summary]

## Execution Steps
1. [Step 1 description and results]
2. [Step 2 description and results]
...

## Final Deliverables
[Deliverable details, links if applicable]

## Issue Resolution (if applicable)
- Problems encountered and resolutions
- Future considerations

## Notes/Improvements
- [Observations or improvement suggestions]
```

## Critical Notes

- **Always confirm unclear points before starting work**
- **Report and obtain approval for important decisions**
- **Report unexpected issues immediately with proposed solutions**
- **Do not make changes not explicitly instructed.** If changes seem necessary, first report as proposal and implement only after approval
- **UI/UX design changes (layout, colors, fonts, spacing) are prohibited** without prior approval with justification
- **Do not change versions specified in tech stack (APIs, frameworks, libraries)** without clear justification and approval
- **Verify arguments and parameters are complete after implementation** as they are often missing
- **Ask for clarification if unclear points or additional requirements arise during implementation**
- **When reporting self-evaluations or investigation findings, only provide responses when you have 90% or higher confidence in the accuracy of the content. If confidence is below this threshold, honestly state that you do not know**

---

Follow these instructions for reliable, high-quality implementation. Process only within specified scope, avoiding unnecessary additions. Always seek confirmation for unclear points or important decisions.

---

*Note: This document is optimized for token efficiency while maintaining all original instructions and intent.*

## Project Structure & Module Organization
- `app/`: FastAPI services and processors. Use `main.py` as entrypoint, define routes in `routes.py`, and keep rule logic confined to `production_date_processor.py` and `macro_production_date_processor.py`.
- `app/master_loader/`: Master data loaders for customers, items, recipes, and deliveries that prepare reference tables before processing.
- `config/`: Runtime configuration (`settings.yaml`), canonical CSV templates, and versioned master datasets; adjust paths here when introducing new control files.
- `data/`: Working area for uploads (`input/`) and generated outputs; treat as ephemeral and keep uncommitted.
- `statics/`: Frontend resources (`html/main.html`, `js/main.js`, `css/`) served alongside the API.
- `docs/`: Authoritative business rules and macro parity studies; review before altering domain logic.

## Build, Test, and Development Commands
- `python -m pip install --user -r requirements_txt.txt` installs runtime and tooling dependencies without creating a virtualenv.
- `uvicorn app.main:app --reload` runs the API locally with autoreload; respect port values in `config/settings.yaml` when testing integrations.
- `python app/main.py` starts the service using production-like defaults.
- `python -m black app` formats Python modules; run before committing.
- `pytest -q` executes the automated suite; pair with targeted fixtures to validate new rules.

## Coding Style & Naming Conventions
- Target Python 3.x, 4-space indentation, and UTF-8 source files.
- Use `snake_case` for modules/functions, `PascalCase` for classes, and `CONSTANT_CASE` for constants.
- Keep processors cohesive: I/O helpers belong in `csv_processor.py`, rule logic in the production processors, and HTTP concerns in `routes.py`.

## Testing Guidelines
- Write tests with `pytest`; name files `test_<module>.py` and functions `test_<behavior>` for discovery.
- Cover new rule branches and data edge cases; mirror scenarios documented in `docs/` to guard parity with business rules.
- When adding fixtures that rely on CSVs, stage them under `data/input/` for local runs and remove them before committing.

## Commit & Pull Request Guidelines
- Craft imperative commits, optionally scoped (e.g., `app: refactor upload handler` or `マクロ条件7の色判定を無効化`).
- PRs should explain purpose, affected modules, and verification steps; link issues and attach UI screenshots when touching `statics/`.
- Update relevant `docs/` sections whenever business rules shift, and call out configuration or master data updates explicitly.

## Security & Configuration Tips
- Never commit real credentials; use placeholders in `config/settings.yaml` and rely on local overrides for secrets.
- Keep master file references synchronized with `config/master_data` and `config/master_files` when onboarding new datasets.
- Logs under `log/` can contain operational data; avoid embedding sensitive values in new log statements.
