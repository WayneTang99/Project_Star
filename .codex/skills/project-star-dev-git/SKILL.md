---
name: project-star-dev-git
description: Sync, commit, and push the dev branch of C:\GodotProject\Project_Star when the user requests Git operations in this project.
---

# Project Star dev Git

Use this workflow only for `C:\GodotProject\Project_Star`. The user's request determines whether to pull, commit, push, or combine them. A request to pull does not authorize a commit or push.

## Branch and safety

- Confirm the repository root, current branch, upstream, and worktree status. The intended branch is `dev`, tracking `origin/dev`. If another branch is checked out, switch to `dev` only when doing so preserves local changes; otherwise ask the user how to handle them.
- Preserve uncommitted work. Do not reset, clean, force push, or silently stash. If a pull cannot fast-forward, inspect divergence and ask before choosing a merge or rebase.
- This project uses `docs/engineering/DEV_CONVENTIONS.md` for commit messages and related Git rules. Read it before creating a commit.

## Pull `dev`

1. Run `git fetch origin dev` and check its exit code. This must reach the server; comparing `HEAD` with a previously cached `origin/dev` does not establish that the project is current.
2. This host may inject `GIT_HTTP_PROXY`, `GIT_HTTPS_PROXY`, `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` as `http://127.0.0.1:9`. If fetch fails through that proxy, clear those variables only for the individual Git command and retry with the tool's appropriate network permission. Do not change global Git or system proxy settings. Report failure if the retry still cannot reach GitHub.
3. With a clean worktree, use `git merge --ff-only origin/dev`. With local changes, first determine whether the update can preserve them; do not overwrite them.
4. Report the fetched remote commit, final local commit, and worktree status. Only say the pull succeeded after the fetch and local update both succeed.

## Commit and push `dev`

1. Inspect the diff and status. Stage only files belonging to the user's requested work; avoid `git add .` when unrelated changes exist.
2. Use a focused commit message following `docs/engineering/DEV_CONVENTIONS.md`. Verify the commit contains the intended files.
3. Before a requested push, fetch `origin dev` successfully again and inspect divergence. If remote `dev` advanced, incorporate it safely before pushing. Push with `git push origin dev`; never force push unless explicitly requested.
4. Check the push exit code and confirm the remote branch matches the new local commit. Report the commit ID and whether the push succeeded.

Treat network or permission errors as failures, not evidence that the branch is already up to date.
