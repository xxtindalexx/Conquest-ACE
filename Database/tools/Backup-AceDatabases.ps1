#Requires -Version 5.1
# ============================================================
# Conquest-ACE Database Backup
# ============================================================
# Edit the variables below, then set this as a Scheduled Task.
# ============================================================

$MYSQLDUMP    = "C:\Program Files\MariaDB 12.2\bin\mysqldump.exe"
$DB_HOST      = "127.0.0.1"
$DB_PORT      = 3306
$DB_USER      = "root"
$DB_PASS      = ""
$DATABASES    = @("ace_auth", "ace_shard", "ace_world", "ace_log")
$BACKUP_ROOT  = "D:\Backups\ACE"
$KEEP_DAYS    = 14          # delete backup folders older than this

# ============================================================

$ErrorActionPreference = "Stop"
$today     = Get-Date -Format "yyyy-MM-dd"
$backupDir = Join-Path $BACKUP_ROOT $today
$logFile   = Join-Path $BACKUP_ROOT "backup.log"

function Log($msg) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $msg"
    Write-Host $line
    Add-Content -LiteralPath $logFile -Value $line -Encoding UTF8
}

New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

Log "===== backup started ====="

# Pass password safely via environment variable
$env:MYSQL_PWD = $DB_PASS

foreach ($db in $DATABASES) {
    $outFile = Join-Path $backupDir "$db`_$today.sql"
    $errFile = "$outFile.err"

    Log "dumping $db..."
    $p = Start-Process -FilePath $MYSQLDUMP `
        -ArgumentList "--host=$DB_HOST","--port=$DB_PORT","--user=$DB_USER",
                      "--single-transaction","--routines","--triggers",
                      "--add-drop-database","--add-drop-table",
                      "--databases",$db `
        -RedirectStandardOutput $outFile `
        -RedirectStandardError  $errFile `
        -NoNewWindow -Wait -PassThru

    $errText = if (Test-Path $errFile) { (Get-Content $errFile -Raw).Trim() } else { "" }
    Remove-Item $errFile -Force -ErrorAction SilentlyContinue

    if ($p.ExitCode -ne 0) {
        Log "  FAILED (exit $($p.ExitCode)): $errText"
        continue
    }
    if ($errText) { Log "  warning: $errText" }

    # Compress to .gz
    $gzFile     = "$outFile.gz"
    $src        = [System.IO.File]::OpenRead($outFile)
    $dst        = [System.IO.File]::Create($gzFile)
    $gz         = [System.IO.Compression.GZipStream]::new($dst, [System.IO.Compression.CompressionMode]::Compress)
    $src.CopyTo($gz)
    $gz.Dispose(); $dst.Dispose(); $src.Dispose()
    Remove-Item $outFile -Force

    $mb = [math]::Round((Get-Item $gzFile).Length / 1MB, 2)
    Log "  OK -> $db`_$today.sql.gz ($mb MB)"
}

Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue

# Delete old backup folders
Get-ChildItem -LiteralPath $BACKUP_ROOT -Directory |
    Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' -and $_.CreationTime -lt (Get-Date).AddDays(-$KEEP_DAYS) } |
    ForEach-Object { Log "removing old backup: $($_.Name)"; Remove-Item $_.FullName -Recurse -Force }

Log "===== backup complete ====="
