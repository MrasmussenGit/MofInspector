
param(
    [string]$OutFile = "MofInspector_source_bundle.txt",
    [string]$Root = ".",
    [switch]$Recurse = $true
)

# Folders to exclude by leaf name
$excludeDirs = @('bin','obj','.git','.vs','.vscode','packages')

# File globs to include and generated files to exclude
$includePatterns = @('*.cs','*.xaml')
$excludeNamePatterns = @('*.g.cs','*.g.i.cs','*.designer.cs')

# Gather files
$all = foreach ($pat in $includePatterns) {
    Get-ChildItem -Path $Root -Filter $pat -File -Recurse:$Recurse
}

# Prune excluded dirs and generated files
$srcFiles = $all |
    Where-Object { $excludeDirs -notcontains $_.Directory.Name } |
    Where-Object {
        $n = $_.Name
        -not ($excludeNamePatterns | ForEach-Object { $n -like $_ } | Where-Object { $_ } )
    }

# Ordering: Program.cs, App.xaml, MainWindow.xaml first; then stable alphabetical
$priorityMap = @{
    'program.cs'      = 0
    'app.xaml'        = 1
    'mainwindow.xaml' = 2
}
$ordered = $srcFiles | Sort-Object {
    $key = $_.Name.ToLowerInvariant()
    if ($priorityMap.ContainsKey($key)) { "{0:D3}" -f $priorityMap[$key] }
    else { "999_" + $_.FullName.ToLowerInvariant() }
}

# Start fresh output
Remove-Item -Path $OutFile -ErrorAction SilentlyContinue
$header = @"
# ========= MofInspector source bundle =========
# Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")
# Root: $(Resolve-Path $Root)
# Files: $($ordered.Count)
# Includes: *.cs, *.xaml
# Excludes: $($excludeDirs -join ', ') ; generated: $($excludeNamePatterns -join ', ')
"@
Set-Content -Path $OutFile -Value $header -Encoding utf8

# Emit contents with markers
foreach ($f in $ordered) {
    $rel = Resolve-Path -Relative $f.FullName
    Add-Content $OutFile ("`n===== BEGIN FILE: {0} =====`n" -f $rel) -Encoding utf8
    Get-Content $f.FullName -Raw | Add-Content $OutFile -Encoding utf8
    Add-Content $OutFile ("`n===== END FILE: {0} =====`n" -f $rel) -Encoding utf8
}
Write-Host "Wrote $($ordered.Count) files"
