#!/usr/bin/env pwsh
# Auto-Assignment Investigation - Log Monitor Script
# This script monitors application logs for [AUTO-ASSIGN] messages in real-time

param(
	[string]$LogFilePath = "$env:LOCALAPPDATA\MMGT\Logs\application.log",
	[int]$RefreshInterval = 2,
	[switch]$ShowAll = $false
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "AUTO-ASSIGNMENT INVESTIGATION - LOG MONITOR" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Monitoring for [AUTO-ASSIGN] messages`n" -ForegroundColor Green
Write-Host "TIP: Submit your test application NOW in another window`n" -ForegroundColor Yellow

# If using Visual Studio Output window, this provides real-time monitoring guidance
Write-Host "INSTRUCTIONS:" -ForegroundColor Cyan
Write-Host "1. Visual Studio Output Window: Search for [AUTO-ASSIGN]" -ForegroundColor White
Write-Host "2. Terminal Output: Run this script and watch for messages" -ForegroundColor White
Write-Host "3. Event Viewer: Check Application Events if on IIS" -ForegroundColor White
Write-Host "`nCapture these log messages and save them for analysis.`n" -ForegroundColor Green

# Create sample log format that you'll see
Write-Host "EXPECTED LOG PATTERNS:" -ForegroundColor Cyan
Write-Host "`n✓ SUCCESS Pattern:" -ForegroundColor Green
Write-Host "  [AUTO-ASSIGN] Starting auto-assignment for application..." -ForegroundColor Green
Write-Host "  [AUTO-ASSIGN-SERVICE] Found X active ADMIN reviewers" -ForegroundColor Green
Write-Host "  [AUTO-ASSIGN] ✓ Successfully auto-assigned application" -ForegroundColor Green

Write-Host "`n✗ FAILURE Pattern:" -ForegroundColor Red
Write-Host "  [AUTO-ASSIGN] Starting auto-assignment for application..." -ForegroundColor Red
Write-Host "  [AUTO-ASSIGN-SERVICE] ✗ No active ADMIN reviewers found!" -ForegroundColor Red
Write-Host "  [AUTO-ASSIGN] ✗ FAILED to auto-assign application" -ForegroundColor Red

Write-Host "`n========================================`n" -ForegroundColor Cyan
Write-Host "Keep this window open while you submit test application." -ForegroundColor Yellow
Write-Host "Copy any [AUTO-ASSIGN] messages you see." -ForegroundColor Yellow
Write-Host "Close this window when done (Ctrl+C)." -ForegroundColor Yellow
Write-Host "`n========================================`n" -ForegroundColor Cyan

Write-Host "Waiting for logs (checking every $RefreshInterval seconds)..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop`n" -ForegroundColor Yellow

# Keep script running for monitoring
$lastPosition = 0
while ($true) {
	try {
		if (Test-Path $LogFilePath) {
			$content = Get-Content $LogFilePath -ErrorAction SilentlyContinue
			if ($content) {
				# Look for AUTO-ASSIGN messages
				$lines = @($content) -split "`n"
				foreach ($line in $lines) {
					if ($line -match "\[AUTO-ASSIGN") {
						if ($line -match "SUCCESS|✓") {
							Write-Host $line -ForegroundColor Green
						} elseif ($line -match "FAIL|✗|ERROR") {
							Write-Host $line -ForegroundColor Red
						} else {
							Write-Host $line -ForegroundColor Yellow
						}
					}
				}
			}
		} else {
			if (-not $showingWarning) {
				Write-Host "Log file not found at: $LogFilePath" -ForegroundColor Yellow
				Write-Host "Visual Studio Output window will show logs in real-time instead." -ForegroundColor Yellow
				$showingWarning = $true
			}
		}
	} catch {
		# Silently continue on read errors
	}

	Start-Sleep -Seconds $RefreshInterval
}
