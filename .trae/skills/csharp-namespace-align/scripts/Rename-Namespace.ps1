# Rename-Namespace.ps1
# Bulk-replace a C# namespace prefix across source files, preserving each
# file's original UTF-8 BOM state and excluding bin/obj generated content.
# Requires PowerShell 5.1 or later.
param(
    [Parameter(Mandatory = $true)]
    [string]$Root,

    [Parameter(Mandatory = $true)]
    [string]$OldNamespace,

    [Parameter(Mandatory = $true)]
    [string]$NewNamespace,

    [string[]]$Include = @('*.cs', '*.razor')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Root)) {
    throw "Root path not found: $Root"
}
if ($OldNamespace -eq $NewNamespace) {
    throw 'OldNamespace and NewNamespace must differ.'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

$changedFiles = 0
$changedHits = 0

Get-ChildItem -Path $Root -Recurse -File -Include $Include |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    ForEach-Object {
        $path = $_.FullName
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $hasBom = $bytes.Length -ge 3 -and
                  $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF

        $text = [System.IO.File]::ReadAllText($path)
        if ($text.Contains($OldNamespace)) {
            $hitCount = ([regex]::Escape($OldNamespace) |
                ForEach-Object { ([regex]::Matches($text, $_)).Count })

            $newText = $text.Replace($OldNamespace, $NewNamespace)
            $encoding = if ($hasBom) { $utf8Bom } else { $utf8NoBom }
            [System.IO.File]::WriteAllText($path, $newText, $encoding)

            $changedFiles++
            $changedHits += $hitCount
        }
    }

Write-Output ("changed files: {0}" -f $changedFiles)
Write-Output ("changed occurrences: {0}" -f $changedHits)
