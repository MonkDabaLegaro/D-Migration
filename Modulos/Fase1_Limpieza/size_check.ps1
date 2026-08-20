$ScanPaths = @("C:\Users\jaime\AppData\Local", "C:\Users\jaime\AppData\Roaming", "C:\ProgramData", "C:\Users\jaime")

$results = @()
foreach ($RootPath in $ScanPaths) {
    if (-not (Test-Path $RootPath)) { continue }
    $dirs = Get-ChildItem -Path $RootPath -Directory -Force -ErrorAction SilentlyContinue
    foreach ($dir in $dirs) {
        $size = 0
        Get-ChildItem -Path $dir.FullName -Recurse -Force -File -ErrorAction SilentlyContinue | ForEach-Object { $size += $_.Length }
        if ($size -gt 1GB) {
            $results += [PSCustomObject]@{
                Path = $dir.FullName
                SizeGB = [math]::Round($size / 1GB, 2)
            }
        }
    }
}

$results | Sort-Object SizeGB -Descending | Select-Object -First 15 | Format-Table -AutoSize
