param(
    [ValidateSet('Compile', 'Test', 'Models', 'Village', 'Map', 'BattleBuild')][string]$Mode = 'Compile',
    [string]$EditorPath = 'D:/Unity/Unity Hub/6000.3.10f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = Join-Path $projectRoot '.claude/cache/design-validation'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$logPath = Join-Path $outputRoot "$Mode-$stamp.log"
$arguments = '-batchmode -projectPath "{0}" -logFile "{1}"' -f $projectRoot, $logPath
switch ($Mode) {
    'Compile' { $arguments += ' -quit' }
    'BattleBuild' { $arguments += ' -quit -executeMethod InsectGame.EditorTools.BattleVisualCaptureBuilder.Build' }
    'Test' {
        $resultPath = Join-Path $outputRoot "PlayMode-$stamp.xml"
        $arguments += ' -runTests -testPlatform PlayMode -testFilter InsectGame.Tests -testResults "{0}"' -f $resultPath
    }
    'Map' {
        $resultPath = Join-Path $projectRoot "Artifacts/map-$stamp"
        $arguments += ' -executeMethod InsectGame.EditorTools.WorldMapDesignCapture.Run -mapCaptureOut "{0}"' -f $resultPath
    }
    'Village' {
        $resultPath = Join-Path $projectRoot "Artifacts/village-$stamp"
        $arguments += ' -executeMethod InsectGame.EditorTools.VillageDesignCapture.Run -villageCaptureOut "{0}"' -f $resultPath
    }
    'Models' {
        $resultPath = Join-Path $projectRoot "Artifacts/models-$stamp"
        $arguments += ' -executeMethod InsectGame.EditorTools.ModelDesignCapture.Run -modelCaptureOut "{0}"' -f $resultPath
    }
}
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $EditorPath
$startInfo.Arguments = $arguments
$startInfo.WorkingDirectory = $projectRoot
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
# Codex may omit this variable; UPM requires it for its global configuration path.
# Restore only the child environment, never machine/user environment settings.
if (-not $startInfo.EnvironmentVariables['ALLUSERSPROFILE']) {
    $startInfo.EnvironmentVariables['ALLUSERSPROFILE'] = [Environment]::GetFolderPath('CommonApplicationData')
}
$editorProcess = [System.Diagnostics.Process]::Start($startInfo)
[pscustomobject]@{ ProcessId = $editorProcess.Id; Log = $logPath; Result = $resultPath }
