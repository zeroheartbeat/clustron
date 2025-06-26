param (
    [string]$Path = "."
)

# Define the license header
$licenseHeader = @"
// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

"@

# Normalize line endings
$licenseHeader = $licenseHeader -replace "`r?`n", "`r`n"

# Get all .cs files recursively
Get-ChildItem -Path $Path -Recurse -Include *.cs | ForEach-Object {
    $file = $_.FullName
    $content = Get-Content -Raw -Path $file

    # Skip if the license already exists
    if ($content -match [regex]::Escape("// Copyright (c) 2025 zeroheartbeat")) {
        Write-Host "Skipping (already has license): $file"
        return
    }

    # Prepend the license and write back
    $newContent = "$licenseHeader$content"
    Set-Content -Path $file -Value $newContent -NoNewline

    Write-Host "Added license to: $file"
}
