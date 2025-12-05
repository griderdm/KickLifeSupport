# Get content, filter for the string, and write to new file
Get-Content .\ksp.log | Where-Object { $_.Contains("[KICKLS]") } | Set-Content .\KickLifeSupportDebug.log

Write-Host "Done. Check KickLifeSupportDebug.log"