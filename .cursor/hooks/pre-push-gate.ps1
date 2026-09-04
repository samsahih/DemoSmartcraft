# Pre-push gate for the Cursor agent.
#
# Blocks any `git push` the agent tries to run unless .cursor/push-approval.json
# exists and names the exact commit currently at HEAD. The approval file is
# written only by the pre-push-review skill after a passing review, and it is
# invalidated automatically by the next commit (the hash no longer matches).
#
# Even when approved, this returns "ask" so the human clicks the final push.
#
# Needs no git binary: it resolves HEAD from the .git folder directly.

$ErrorActionPreference = 'Stop'

function Emit([string]$permission, [string]$userMessage, [string]$agentMessage) {
    @{
        permission    = $permission
        user_message  = $userMessage
        agent_message = $agentMessage
    } | ConvertTo-Json -Compress
    exit 0
}

function Resolve-HeadSha([string]$repoRoot) {
    $gitDir = Join-Path $repoRoot '.git'
    if (Test-Path -LiteralPath $gitDir -PathType Leaf) {
        # Worktree: .git is a file "gitdir: <path>"
        $line = (Get-Content -LiteralPath $gitDir -Raw).Trim()
        if ($line -match '^gitdir:\s*(.+)$') { $gitDir = $Matches[1].Trim() }
    }
    $headPath = Join-Path $gitDir 'HEAD'
    if (-not (Test-Path -LiteralPath $headPath)) { return $null }

    $head = (Get-Content -LiteralPath $headPath -Raw).Trim()
    if ($head -match '^[0-9a-f]{40}$') { return $head }           # detached HEAD
    if ($head -notmatch '^ref:\s*(.+)$') { return $null }
    $ref = $Matches[1].Trim()

    $refPath = Join-Path $gitDir ($ref -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (Test-Path -LiteralPath $refPath) {
        return (Get-Content -LiteralPath $refPath -Raw).Trim()
    }
    $packed = Join-Path $gitDir 'packed-refs'
    if (Test-Path -LiteralPath $packed) {
        foreach ($l in Get-Content -LiteralPath $packed) {
            if ($l -match "^([0-9a-f]{40})\s+$([regex]::Escape($ref))$") { return $Matches[1] }
        }
    }
    return $null
}

$raw = [Console]::In.ReadToEnd()
$command = ''
try {
    $payload = $raw | ConvertFrom-Json
    if ($null -ne $payload.command) { $command = [string]$payload.command }
} catch { }

# Only gate pushes. Everything else passes untouched.
if ($command -notmatch '(^|[\s;&|])git\s+([^\s;&|]+\s+)*push(\s|$)') {
    Emit 'allow' '' ''
}

$repoRoot = (Get-Location).Path
$approvalPath = Join-Path $repoRoot '.cursor\push-approval.json'
$howTo = 'Run the pre-push-review skill (.cursor/skills/pre-push-review/SKILL.md). It reviews the code, and only if the verdict is READY does it write .cursor/push-approval.json for the current commit. Then retry the push.'

$headSha = Resolve-HeadSha $repoRoot
if (-not $headSha) {
    Emit 'deny' 'Push blocked: could not resolve the current commit from .git/HEAD.' "Push blocked. Could not read HEAD from .git. Make sure there is at least one commit. $howTo"
}

if (-not (Test-Path -LiteralPath $approvalPath)) {
    Emit 'deny' 'Push blocked: no pre-push review approval found for this commit.' "Push blocked: no pre-push review on record. $howTo"
}

try {
    $approval = Get-Content -LiteralPath $approvalPath -Raw | ConvertFrom-Json
} catch {
    Emit 'deny' 'Push blocked: .cursor/push-approval.json is not valid JSON.' "Push blocked: approval file unreadable. Delete it and re-run the review. $howTo"
}

if ([string]$approval.verdict -ne 'READY') {
    Emit 'deny' "Push blocked: last review verdict was '$($approval.verdict)', not READY." "Push blocked: the last review did not pass. Fix the listed blockers, commit, and re-run the review. $howTo"
}

if ([string]$approval.commit -ne $headSha) {
    Emit 'deny' "Push blocked: review approval is for commit $([string]$approval.commit), but HEAD is $headSha." "Push blocked: the code changed since the last review (approval is for $([string]$approval.commit), HEAD is $headSha). Re-run the review on the current commit. $howTo"
}

$short = $headSha.Substring(0, 8)
$when = [string]$approval.reviewedAt
Emit 'ask' "Pre-push review passed for commit $short (reviewed $when). Push now?" "Approval matches HEAD $short. Human confirmation requested."
