$ErrorActionPreference = 'Stop'

$project = 'C:\MSSA Code-github\REPO_SuperBallEnemy\Plugin\RepoSuperBallEnemy\RepoSuperBallEnemy.csproj'
$sourceDll = 'C:\MSSA Code-github\REPO_SuperBallEnemy\Plugin\RepoSuperBallEnemy\bin\Debug\net48\RepoSuperBallEnemy.dll'
$pluginDir = 'D:\SteamLibrary\steamapps\common\REPO\BepInEx\plugins\RepoSuperBallEnemy'
$targetDll = Join-Path $pluginDir 'RepoSuperBallEnemy.dll'

dotnet build $project

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -Path $sourceDll -Destination $targetDll -Force

Get-Item -Path $targetDll | Select-Object FullName, Length, LastWriteTime
