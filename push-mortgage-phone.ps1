# Commit and push the Mortgage phone number change (steps M4 + M7).
# Run from anywhere inside the Mortgage repo:
#   powershell -ExecutionPolicy Bypass -File .\push-mortgage-phone.ps1
# It stops without committing if: you are on main/master, or any file other than the four expected ones has changed.
param([string]$GitHubUrl = '')   # e.g. -GitHubUrl https://github.com/<owner>/<repo>.git
$ErrorActionPreference = 'Stop'

$root = (git rev-parse --show-toplevel 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Not inside a git repository. Run it from the accessmortage-backend_live folder. ($root)" }
Set-Location $root
$branch = (git branch --show-current).Trim()
Write-Host "Repository : $root"
Write-Host "Branch     : $branch"
Write-Host "Last commit: $(git log -1 --format='%h %ad %s' --date=short)"
Write-Host 'Remotes    :'
git remote -v

$remoteUrl = (git remote get-url origin).Trim()
if ($remoteUrl -notmatch 'accessmortgage|accessmortage-backend') {
    throw "origin is '$remoteUrl', which is not the Mortgage backend repository. Nothing was changed."
}
if ($branch -eq '') { throw 'Detached HEAD. Check out a branch first.' }
# Main lines are never committed to directly: the changes go on a new branch and are merged by pull request.
$protectedBranches = @('main','master','accessPBackend')
$newBranch = 'feature/mortgage-phone-number'

$expected = @('CreateApplicationRequest.cs','UpdateApplicationRequest.cs','ApplicationService.cs','PhoneNumberNormalizer.cs')
$lines = @(git status --porcelain)
if ($lines.Count -eq 0) { throw 'Nothing to commit. The changes may already be committed.' }

$mine = @(); $others = @()
foreach ($l in $lines) {
    $path = $l.Substring(3).Trim('"')
    if ((Split-Path $path -Leaf) -eq 'push-mortgage-phone.ps1') { continue }
    if ($expected -contains (Split-Path $path -Leaf)) { $mine += $path } else { $others += $l }
}
Write-Host ''
Write-Host 'Phone change files:' -ForegroundColor Cyan
$mine | ForEach-Object { Write-Host "  $_" }
if ($mine.Count -ne 4) { throw "Expected 4 changed files from M4 + M7, found $($mine.Count)." }
if ($others.Count -gt 0) {
    Write-Host ''
    Write-Host 'Other changes found in the folder:' -ForegroundColor Yellow
    $others | ForEach-Object { Write-Host "  $_" }
    Write-Host 'These stay as they are on your disk and are NOT committed: only the 4 phone files above are added by name.' -ForegroundColor Yellow
    if ($others -match 'appsettings') { Write-Host 'appsettings.json is left out on purpose (it holds database passwords and the JWT key).' -ForegroundColor Yellow }
}

$target = $branch
if ($protectedBranches -contains $branch) { $target = $newBranch }
$pushRemote = 'origin'
if ($GitHubUrl -ne '') {
    if ($GitHubUrl -notmatch '^https://github\.com/[^/]+/[^/]+?(\.git)?/?$') { throw "That is not a GitHub repository address: $GitHubUrl" }
    $pushRemote = 'github'
}
$dest = if ($pushRemote -eq 'github') { "GitHub ($GitHubUrl)" } else { 'origin (Azure DevOps)' }
$answer = Read-Host "Commit these 4 files on '$target' (from '$branch') and push to ${dest}? (y/n)"
if ($answer -ne 'y') { Write-Host 'Cancelled. Nothing changed.'; exit 0 }

if ($target -ne $branch) {
    git checkout -b $target
    if ($LASTEXITCODE -ne 0) { throw "Could not create branch '$target' (it may already exist). Nothing was committed." }
    $branch = $target
}

git add -- $mine
$msg = @'
Borrower phone number: accept autofilled numbers, save as 234XXXXXXXXXX

- Create: spaces, dashes and brackets are removed before the check; the check is anchored and accepts Nigerian mobile numbers only
- Numbers are saved as 234XXXXXXXXXX on create and edit (7066088021, 07066088021 and +2347066088021 give 2347066088021)
- Edit: the phone number is now cleaned and checked like on create
- New PhoneNumberNormalizer shared by both

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01DibWXDZKsrU1v2mPyXq9aj
'@
$tmp = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmp, $msg, (New-Object System.Text.UTF8Encoding($false)))
git commit -F $tmp
$code = $LASTEXITCODE
Remove-Item $tmp -Force
if ($code -ne 0) { throw 'Commit failed.' }

if ($pushRemote -eq 'github') {
    $existing = @(git remote) -contains 'github'
    if ($existing) {
        $have = (git remote get-url github).Trim()
        if ($have -ne $GitHubUrl) { throw "A remote named 'github' already points to $have, not $GitHubUrl. Nothing was pushed (the commit is saved locally)." }
    } else {
        git remote add github $GitHubUrl
    }
}
git push -u $pushRemote $branch
if ($LASTEXITCODE -ne 0) { throw 'Push failed. The commit is saved locally. Send me the message above.' }

Write-Host ''
Write-Host "Pushed to $pushRemote/$branch." -ForegroundColor Green
git log -1 --format='%h %s'
