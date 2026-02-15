# ACE Test Server Setup Guide

## Overview
This guide walks through setting up a secondary ACE server environment on the same Windows PC as your production server. The test server will use:
- **MySQL Port:** 3307 (instead of 3306)
- **Game Ports:** 9100/9101 (instead of 9000/9001)

This allows both servers to run simultaneously without conflicts.

---

## Part 1: MySQL Secondary Instance Setup

### Option A: Run Second MySQL Instance (Recommended)

#### Step 1: Create Directory Structure for Second Instance

```powershell
# Open PowerShell as Administrator and run:
mkdir "C:\MySQL_Test"
mkdir "C:\MySQL_Test\data"
mkdir "C:\MySQL_Test\logs"
```

#### Step 2: Create Configuration File for Test Instance

Create a new file at `C:\MySQL_Test\my_test.ini` with the following content:

```ini
[mysqld]
# Basic Settings
basedir=C:/Program Files/MySQL/MySQL Server 8.0
datadir=C:/MySQL_Test/data
port=3307
socket=MySQL_Test

# Use a different named pipe
enable-named-pipe
shared-memory
shared-memory-base-name=MySQL_Test

# Error logging
log-error=C:/MySQL_Test/logs/error.log

# InnoDB settings
innodb_data_home_dir=C:/MySQL_Test/data
innodb_log_group_home_dir=C:/MySQL_Test/data

# Performance settings (adjust based on available RAM)
innodb_buffer_pool_size=512M
max_connections=100

# Character set
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci

[client]
port=3307
socket=MySQL_Test
```

> **Note:** Adjust `basedir` to match your MySQL installation path. Common paths:
> - `C:/Program Files/MySQL/MySQL Server 8.0`
> - `C:/Program Files/MySQL/MySQL Server 8.4`

#### Step 3: Initialize the Test Database

```powershell
# Open PowerShell as Administrator
cd "C:\Program Files\MySQL\MySQL Server 8.0\bin"

# Initialize the data directory (this creates system tables)
.\mysqld.exe --defaults-file="C:\MySQL_Test\my_test.ini" --initialize-insecure --console

# The --initialize-insecure creates root with no password (we'll set one later)
```

#### Step 4: Install as Windows Service

```powershell
# Install the test instance as a separate Windows service
.\mysqld.exe --install MySQL_Test --defaults-file="C:\MySQL_Test\my_test.ini"

# Start the service
net start MySQL_Test
```

#### Step 5: Secure the Test Instance

```powershell
# Connect to the test instance
.\mysql.exe -u root -P 3307

# In the MySQL prompt, set a root password:
ALTER USER 'root'@'localhost' IDENTIFIED BY 'YourTestRootPassword';
FLUSH PRIVILEGES;
EXIT;
```

---

### Option B: Use Same MySQL Instance, Different Databases (Simpler)

If you don't want to run two MySQL instances, you can use the same MySQL server (port 3306) but with different database names:

- Production: `ace_auth`, `ace_shard`, `ace_world`
- Test: `ace_auth_test`, `ace_shard_test`, `ace_world_test`

Skip to **Part 2** if using this approach, but adjust database names accordingly.

---

## Part 2: Create ACE Databases for Test Server

### Step 1: Connect to Test MySQL Instance

```powershell
cd "C:\Program Files\MySQL\MySQL Server 8.0\bin"
.\mysql.exe -u root -p -P 3307
```

### Step 2: Create Databases and User

```sql
-- Create the three ACE databases
CREATE DATABASE ace_auth_test;
CREATE DATABASE ace_shard_test;
CREATE DATABASE ace_world_test;

-- Create a dedicated user for the test server
CREATE USER 'ace_test'@'localhost' IDENTIFIED BY 'YourTestACEPassword';

-- Grant permissions
GRANT ALL PRIVILEGES ON ace_auth_test.* TO 'ace_test'@'localhost';
GRANT ALL PRIVILEGES ON ace_shard_test.* TO 'ace_test'@'localhost';
GRANT ALL PRIVILEGES ON ace_world_test.* TO 'ace_test'@'localhost';
FLUSH PRIVILEGES;

-- Verify databases were created
SHOW DATABASES;
EXIT;
```

### Step 3: Import Database Schemas

You have two options for the test database content:

#### Option A: Fresh Database (Start Clean)

```powershell
cd "C:\Users\xxtin\Documents\ACEMain\Database\Base"

# Import base schemas to test databases
# Note: Use -P 3307 to connect to test instance
mysql -u ace_test -p -P 3307 ace_auth_test < AuthenticationBase.sql
mysql -u ace_test -p -P 3307 ace_shard_test < ShardBase.sql
mysql -u ace_test -p -P 3307 ace_world_test < WorldBase.sql
```

Then import world data:
```powershell
cd "C:\Users\xxtin\Documents\ACEMain\Database\World"

# Import world data (this may take a while)
# Combine all SQL files or import individually
Get-ChildItem -Filter "*.sql" | ForEach-Object {
    Write-Host "Importing $($_.Name)..."
    mysql -u ace_test -p -P 3307 ace_world_test < $_.FullName
}
```

#### Option B: Clone Production Database (Recommended for Testing)

This copies your production data to test, so you can test with real data:

```powershell
cd "C:\Program Files\MySQL\MySQL Server 8.0\bin"

# Export production databases
.\mysqldump.exe -u root -p -P 3306 ace_auth > C:\MySQL_Test\ace_auth_backup.sql
.\mysqldump.exe -u root -p -P 3306 ace_shard > C:\MySQL_Test\ace_shard_backup.sql
.\mysqldump.exe -u root -p -P 3306 ace_world > C:\MySQL_Test\ace_world_backup.sql

# Import to test databases
.\mysql.exe -u ace_test -p -P 3307 ace_auth_test < C:\MySQL_Test\ace_auth_backup.sql
.\mysql.exe -u ace_test -p -P 3307 ace_shard_test < C:\MySQL_Test\ace_shard_backup.sql
.\mysql.exe -u ace_test -p -P 3307 ace_world_test < C:\MySQL_Test\ace_world_backup.sql
```

---

## Part 3: ACE Server Configuration for Test Environment

### Step 1: Create Test Server Directory

```powershell
# Copy your ACE server binaries to a test folder
# Assuming your production server is at C:\ACE\Server
xcopy "C:\ACE\Server" "C:\ACE\TestServer" /E /I /H

# Or if building from source, publish to a separate folder
cd "C:\Users\xxtin\Documents\ACEMain\Source\ACE.Server"
dotnet publish -c Release -o "C:\ACE\TestServer"
```

### Step 2: Configure Test Server Settings

Edit `C:\ACE\TestServer\Config.js` (create if it doesn't exist):

```json
{
    "Server": {
        "WorldName": "Conquest TEST",
        "WelcomeMessage": "Welcome to Conquest TEST Server!",

        "Network": {
            "Host": "0.0.0.0",
            "Port": 9100
        },

        "Accounts": {
            "OverrideAccountDefaults": true,
            "DefaultAccessLevel": 5,
            "AllowAutoAccountCreation": true,
            "AutoAccountCreationPassword": ""
        },

        "WorldDatabasePrecaching": true,
        "ShardDatabasePrecaching": true,

        "Threading": {
            "LandblockManagerParallelOptions": {
                "MaxDegreeOfParallelism": -1
            },
            "NetworkManagerParallelOptions": {
                "MaxDegreeOfParallelism": -1
            },
            "DatabaseManagerParallelOptions": {
                "MaxDegreeOfParallelism": -1
            }
        },

        "ShutdownInterval": 60,

        "DatFilesDirectory": "C:\\ACE\\Dats",

        "ServerPerformanceMonitorAutoStart": false,

        "WorldDatabasePrecachingMaxParallelism": -1
    },

    "MySql": {
        "Authentication": {
            "Host": "127.0.0.1",
            "Port": 3307,
            "Database": "ace_auth_test",
            "Username": "ace_test",
            "Password": "YourTestACEPassword"
        },
        "Shard": {
            "Host": "127.0.0.1",
            "Port": 3307,
            "Database": "ace_shard_test",
            "Username": "ace_test",
            "Password": "YourTestACEPassword"
        },
        "World": {
            "Host": "127.0.0.1",
            "Port": 3307,
            "Database": "ace_world_test",
            "Username": "ace_test",
            "Password": "YourTestACEPassword"
        }
    },

    "Offline": {
        "PurgeDeletedCharacters": false,
        "PurgeOrphanedBiotas": false
    },

    "DDD": {
        "EnableDATPatching": true
    }
}
```

### Step 3: Verify Port Configuration

Make sure the following settings are correct in your test `Config.js`:

| Setting | Production | Test |
|---------|------------|------|
| Network.Port | 9000 | 9100 |
| MySql.*.Port | 3306 | 3307 |
| MySql.*.Database | ace_* | ace_*_test |

---

## Part 4: Firewall Configuration

### Open Ports for Test Server

```powershell
# Run PowerShell as Administrator

# Open UDP port 9100 for test server connections
New-NetFirewallRule -DisplayName "ACE Test Server UDP 9100" -Direction Inbound -Protocol UDP -LocalPort 9100 -Action Allow

# Open UDP port 9101 for test server connections
New-NetFirewallRule -DisplayName "ACE Test Server UDP 9101" -Direction Inbound -Protocol UDP -LocalPort 9101 -Action Allow

# If MySQL test instance needs external access (usually not needed)
# New-NetFirewallRule -DisplayName "MySQL Test 3307" -Direction Inbound -Protocol TCP -LocalPort 3307 -Action Allow
```

---

## Part 5: Running the Test Server

### Start MySQL Test Instance (if using Option A)

```powershell
# If not already running
net start MySQL_Test

# Verify it's running
Get-Service MySQL_Test
```

### Start ACE Test Server

```powershell
cd "C:\ACE\TestServer"
.\ACE.Server.exe
```

Or create a batch file `StartTestServer.bat`:

```batch
@echo off
title ACE Test Server - Conquest TEST
cd /d "C:\ACE\TestServer"
ACE.Server.exe
pause
```

### Verify Server Started Successfully

Look for these messages in the console:
```
[SERVER] WorldName: Conquest TEST
[NETWORK] Binding to 0.0.0.0:9100
[DATABASE] Connected to ace_auth_test on 127.0.0.1:3307
[DATABASE] Connected to ace_shard_test on 127.0.0.1:3307
[DATABASE] Connected to ace_world_test on 127.0.0.1:3307
```

---

## Part 6: Connecting to Test Server

### Configure AC Client

Create a new shortcut or batch file to connect to the test server:

```batch
@echo off
cd /d "C:\Games\Asheron's Call"
start acclient.exe -h 127.0.0.1 -p 9100 -a YOUR_ACCOUNT -w PASSWORD
```

Or modify your ThwargLauncher / Decal settings:
- **Server Address:** 127.0.0.1 (or your server's IP)
- **Port:** 9100

### For External Connections

If others need to connect to your test server:
1. Use your public IP or domain
2. Port forward 9100/9101 UDP on your router
3. Players connect to `your.ip.address:9100`

---

## Part 7: Managing Both Servers

### Quick Reference

| Component | Production | Test |
|-----------|------------|------|
| MySQL Service | MySQL80 (or MySQL) | MySQL_Test |
| MySQL Port | 3306 | 3307 |
| Game Port | 9000/9001 | 9100/9101 |
| Server Directory | C:\ACE\Server | C:\ACE\TestServer |
| Auth Database | ace_auth | ace_auth_test |
| Shard Database | ace_shard | ace_shard_test |
| World Database | ace_world | ace_world_test |

### Useful Commands

```powershell
# Check MySQL services
Get-Service | Where-Object {$_.Name -like "*MySQL*"}

# Start/Stop Production MySQL
net start MySQL80
net stop MySQL80

# Start/Stop Test MySQL
net start MySQL_Test
net stop MySQL_Test

# Quick connect to test MySQL
mysql -u ace_test -p -P 3307

# Quick connect to production MySQL
mysql -u root -p -P 3306
```

### Sync World Database Changes

When you want to apply world database updates from production to test:

```powershell
# Export specific tables or full world DB from production
mysqldump -u root -p -P 3306 ace_world weenie > weenie_update.sql

# Import to test
mysql -u ace_test -p -P 3307 ace_world_test < weenie_update.sql
```

---

## Part 8: Troubleshooting

### MySQL Test Instance Won't Start

1. Check error log: `C:\MySQL_Test\logs\error.log`
2. Verify data directory was initialized
3. Ensure port 3307 isn't in use: `netstat -an | findstr 3307`
4. Check Windows Event Viewer for service errors

### ACE Server Can't Connect to Database

1. Verify MySQL test service is running
2. Test manual connection: `mysql -u ace_test -p -P 3307`
3. Check Config.js for correct port and credentials
4. Verify databases exist: `SHOW DATABASES;`

### Players Can't Connect

1. Check firewall rules are created
2. Verify server is listening: `netstat -an | findstr 9100`
3. Test local connection first (127.0.0.1)
4. If external, verify port forwarding on router

### Port Conflicts

If either port is already in use:
```powershell
# Find what's using port 9100
netstat -ano | findstr 9100
# Then lookup the process ID
tasklist | findstr <PID>
```

---

## Part 9: Automating Startup (Optional)

### Create Windows Task Scheduler Tasks

To auto-start the test server on system boot:

1. Open Task Scheduler
2. Create Basic Task: "ACE Test Server"
3. Trigger: "When the computer starts"
4. Action: Start a program
   - Program: `C:\ACE\TestServer\ACE.Server.exe`
   - Start in: `C:\ACE\TestServer`
5. Check "Run whether user is logged on or not"

### Create a Management Script

Save as `C:\ACE\ManageServers.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("start-test", "stop-test", "restart-test", "status")]
    $Action
)

switch ($Action) {
    "start-test" {
        Write-Host "Starting MySQL Test..."
        Start-Service MySQL_Test
        Start-Sleep -Seconds 3
        Write-Host "Starting ACE Test Server..."
        Start-Process -FilePath "C:\ACE\TestServer\ACE.Server.exe" -WorkingDirectory "C:\ACE\TestServer"
        Write-Host "Test server started!"
    }
    "stop-test" {
        Write-Host "Stopping ACE Test Server..."
        Get-Process | Where-Object {$_.Path -like "*TestServer*"} | Stop-Process -Force
        Write-Host "Test server stopped!"
    }
    "restart-test" {
        & $PSCommandPath -Action stop-test
        Start-Sleep -Seconds 2
        & $PSCommandPath -Action start-test
    }
    "status" {
        Write-Host "`n=== MySQL Services ===" -ForegroundColor Cyan
        Get-Service | Where-Object {$_.Name -like "*MySQL*"} | Format-Table Name, Status

        Write-Host "`n=== ACE Processes ===" -ForegroundColor Cyan
        Get-Process | Where-Object {$_.Name -eq "ACE.Server"} | Format-Table Id, ProcessName, Path

        Write-Host "`n=== Port Usage ===" -ForegroundColor Cyan
        netstat -an | Select-String "9000|9100|3306|3307"
    }
}
```

Usage:
```powershell
.\ManageServers.ps1 -Action status
.\ManageServers.ps1 -Action start-test
.\ManageServers.ps1 -Action stop-test
```

---

## Summary Checklist

- [ ] MySQL test instance installed on port 3307
- [ ] Test databases created (ace_auth_test, ace_shard_test, ace_world_test)
- [ ] Test user created with proper permissions
- [ ] ACE Server copied to test directory
- [ ] Config.js configured with test ports and databases
- [ ] Firewall rules added for ports 9100/9101
- [ ] Test server starts successfully
- [ ] Can connect to test server from client
- [ ] Both servers can run simultaneously

---

*Document created for Conquest server test environment setup.*
