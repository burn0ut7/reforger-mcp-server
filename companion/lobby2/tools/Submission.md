# Mission submission prototype

GC Tools > Submit mission prepares an approval request for contributors who have
read access to GC Missions and a GitHub account, but no write access to the GC
repository. The first use may require browser sign-in through Git Credential
Manager. The user supplies a summary and testing/reviewer details; Git mechanics
are handled by the tool.

## Submission ownership

- `GC_MissionSubmission.c` saves the open mission and refreshes its mission data.
- `GC-SubmitMission.ps1` snapshots only that world's `.ent`, `.meta`, matching
  mission header and metadata, and `_Layers` directory. Other local changes are
  displayed as excluded and require acknowledgement. Additional prefab/assets
  outside this scope need a separate submission; the prototype does not infer
  their dependency graph.
- The author checkout's HEAD, branch, real index, and working files stay unchanged.
  A temporary index builds the scoped tree on the shared ancestor. This prevents
  unrelated committed changes on the author's branch from entering the request.
- The first submission generates `mission/<mission-name>-<world-guid>` in the
  author's own fork. The hidden name is retained in local Git configuration.
  Saved source/submission refs preserve retry state and previous resolutions.
  No upstream branch is pushed, no force push is used, and nothing auto-merges.
- An existing open request is updated. Fork creation and readiness are handled by
  the tool. The final screen reports awaiting approval, a known conflict, or a
  pending GitHub conflict check; an unknown check is never presented as clean.

## Conflicts and interrupted operations

`GC-Review.ps1` uses `git merge-tree` to detect overlapping edits without starting a
merge in the author's checkout. Conflicting text files show read-only My version
and Shared version previews with explicit whole-file choices and Cancel. Chosen
content goes into the submission only. The world/mission-header resource linkage
and required files are checked again after conflict resolution.

The prototype stops safely for binary, structural, unreadable, or over-12KB file
conflicts and asks for a mission reviewer. It does not guess a winning version or
provide a line-by-line editor. Submissions changed on another computer also stop
for review. Existing unfinished Git operations require human assistance.

Changes to the local mission, shared base or existing request during review stop
submission and require a fresh review. Prepared objects are retained before push,
so rejected or uncertain uploads can be retried without force-overwriting history.
The submission mutex prevents two tool windows from preparing the same mission.

Committed mod-update conflicts use the same review surface; applying an accepted
update changes the mod checkout. Overlapping uncommitted files require explicit
Back up my files and update, using the existing selective backup mechanism.
Skipping/cancelling keeps local changes. Backup recovery is not yet a UI workflow.

## Validation

Run under Windows PowerShell 5.1:

```powershell
powershell -NoProfile -File companion/lobby2/tools/tests/Submission.Tests.ps1
powershell -NoProfile -File companion/lobby2/tools/tests/ModUpdates.Tests.ps1
```

These use real isolated local Git repositories and a bare personal fork; only
GitHub API responses and dialog choices are substituted. They do not create live
GitHub forks or pull requests. Coverage includes scope/isolation, repeat requests,
cancellation, both conflict choices, retained resolutions, review races, rejected
push retry, external submission edits, selective backups and update conflicts.
Live GitHub sign-in/API permissions still need a contributor-account acceptance
pass. Private repositories must permit the contributor to read and fork them.

Protocol references: https://docs.github.com/en/rest/repos/forks,
https://docs.github.com/en/rest/pulls/pulls, https://git-scm.com/docs/git-merge-tree.
