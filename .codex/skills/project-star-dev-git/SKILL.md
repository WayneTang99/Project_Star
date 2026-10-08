---
name: project-star-dev-git
description: Sync, commit, and push the dev branch of D:\GodotSource\Project_Star when the user requests Git operations in this project.
---

# Project Star dev Git

Use this workflow only for `D:\GodotSource\Project_Star`. The user's request determines whether to pull, commit, push, or combine them. A request to pull does not authorize a commit or push.

## Branch and safety

- Confirm the repository root, current branch, upstream, and worktree status. The intended branch is `dev`, tracking `origin/dev`. If another branch is checked out, switch to `dev` only when doing so preserves local changes; otherwise ask the user how to handle them.
- Preserve uncommitted work. Do not reset, clean, force push, or silently stash. If a pull cannot fast-forward, inspect divergence and ask before choosing a merge or rebase.
- This project uses `docs/engineering/DEV_CONVENTIONS.md` for commit messages and related Git rules. Read it before creating a commit.

## Pull `dev`

1. Run `git fetch origin dev` and check its exit code. This must reach the server; comparing `HEAD` with a previously cached `origin/dev` does not establish that the project is current.
2. This host may inject `GIT_HTTP_PROXY`, `GIT_HTTPS_PROXY`, `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` as `http://127.0.0.1:9`. If fetch fails through that proxy, clear those variables only for the individual Git command and retry with the tool's appropriate network permission. Do not change global Git or system proxy settings. Report failure if the retry still cannot reach GitHub.
3. With a clean worktree, use `git merge --ff-only origin/dev`. With local changes, first determine whether the update can preserve them; do not overwrite them.
4. After the local update, run `git ls-remote --exit-code origin refs/heads/dev` to query the server directly. Require exit code 0 and exactly one full SHA for `refs/heads/dev`; empty output, incomplete/pending output, or a failed command is not verification. Compare that SHA with both `git rev-parse HEAD` and `git rev-parse refs/remotes/origin/dev`.
5. If the server SHA differs from the cached remote SHA, fetch and safely fast-forward again, then repeat the direct server check. Limit this to two additional synchronization attempts; if the remote keeps advancing or commands fail, report the observed SHAs and the unresolved condition instead of claiming completion. If HEAD has local commits ahead of the server, report that explicitly rather than saying the branches match.
6. Only report “up to date” or “synchronized” when the server SHA, cached remote SHA, and local HEAD all match at the final check. Include the verified commit and worktree status. A silent fetch, `Already up to date`, or equality of HEAD and cached origin/dev alone never establishes that the server is current.

## Commit and push `dev`

1. Inspect the diff and status. Stage only files belonging to the user's requested work; avoid `git add .` when unrelated changes exist.
2. Use a focused commit message following `docs/engineering/DEV_CONVENTIONS.md`. Verify the commit contains the intended files.
3. Before a requested push, fetch `origin dev` successfully again and inspect divergence. If remote `dev` advanced, incorporate it safely before pushing. Push with `git push origin dev`; never force push unless explicitly requested.
4. Check the push exit code, then directly query `git ls-remote --exit-code origin refs/heads/dev` and compare its full SHA with HEAD. Report synchronization only when they match; on mismatch or failed verification, report the successful push separately from the unconfirmed current remote state.

Treat network or permission errors as failures, not evidence that the branch is already up to date.
