---
mode: agent
---

Regenerate the challenges SQL from the markdown files and produce two scripts:

1. **Reset script** – delete all existing challenges, clear all participant scores and leaderboard entries, and reset the Activities table to default weights. Leave Teams and Participants untouched.
2. **Insert script** – run `src/challenges-util/generate_sql_inserts.py` to convert every markdown file in `src/challenges-markdown/` into SQL INSERT statements and write the output to `src/challenges-util/challenges-insert.sql`.
