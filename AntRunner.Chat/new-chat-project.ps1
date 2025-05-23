Set-StrictMode -Version Latest

# Check for PowerShell Core
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Write-Host "Error: This script requires PowerShell Core (version 6 or later). Exiting." -ForegroundColor Red
    exit 1
}

# Define required variables
$requiredEnvVars = @(
    "TARGET_DIRECTORY",
    "AZURE_OPENAI_RESOURCE",
    "AZURE_OPENAI_API_KEY",
    "AZURE_OPENAI_DEPLOYMENT",
    "AZURE_OPENAI_API_VERSION"
)

function Load-EnvFile {
    param (
        [string]$FilePath
    )

    $envVars = @{}

    if (-Not (Test-Path $FilePath)) {
        Write-Host "No .env file found at $FilePath. You will be prompted for values." -ForegroundColor Yellow
        return $envVars
    }

    Get-Content $FilePath | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -match '^\s*$') { return }
        if ($_ -match '^\s*([^=]+?)\s*=\s*(.*)\s*$') {
            $key = $matches[1].Trim()
            $val = $matches[2].Trim()
            $envVars[$key] = $val
        }
    }

    Write-Host ".env file loaded." -ForegroundColor Green
    return $envVars
}

function Prompt-EnvVar {
    param(
        [string]$VarName,
        [string]$CurrentValue
    )
    if ([string]::IsNullOrWhiteSpace($CurrentValue)) {
        Write-Host "$VarName is not set." -ForegroundColor Yellow
    } else {
        Write-Host "Current value of $VarName is: $CurrentValue" -ForegroundColor Cyan
    }
    $inputValue = Read-Host "Enter new value for $VarName (leave blank to keep current)"
    if ([string]::IsNullOrWhiteSpace($inputValue)) {
        return $CurrentValue
    } else {
        return $inputValue
    }
}

function Prompt-VolumePath {
    param(
        [string]$CurrentPath
    )
    if ([string]::IsNullOrWhiteSpace($CurrentPath)) {
        Write-Host "Current volume path is not set." -ForegroundColor Yellow
    } else {
        Write-Host "Current volume path: $CurrentPath" -ForegroundColor Cyan
    }
    $inputPath = Read-Host "Enter new volume path (leave blank to keep current)"
    if ([string]::IsNullOrWhiteSpace($inputPath)) {
        return $CurrentPath
    } else {
        return $inputPath
    }
}

# Get script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Load .env file if present
$envFilePath = Join-Path $scriptDir ".env"
$envVars = Load-EnvFile -FilePath $envFilePath

# Prompt for any missing required values
foreach ($varName in $requiredEnvVars) {
    if (-not $envVars.ContainsKey($varName) -or [string]::IsNullOrWhiteSpace($envVars[$varName])) {
        $envVars[$varName] = Prompt-EnvVar -VarName $varName -CurrentValue ""
    }
}

# Validate all values are now populated
$missingVars = @($requiredEnvVars | Where-Object { [string]::IsNullOrWhiteSpace($envVars[$_]) })
if ($missingVars.Count -gt 0) {
    Write-Host "Missing required variables: $($missingVars -join ', ')" -ForegroundColor Red
    exit 1
}

# Extract final values
$targetDir            = $envVars["TARGET_DIRECTORY"]
$azureOpenAIResource   = $envVars["AZURE_OPENAI_RESOURCE"]
$azureOpenAIApiKey     = $envVars["AZURE_OPENAI_API_KEY"]
$azureOpenAIDeployment = $envVars["AZURE_OPENAI_DEPLOYMENT"]
$azureOpenAIApiVersion = $envVars["AZURE_OPENAI_API_VERSION"]

# Step 1: Ensure target directory exists
if ([string]::IsNullOrWhiteSpace($targetDir)) {
    Write-Host "Target directory is required. Exiting." -ForegroundColor Red
    exit 1
}
if (-Not (Test-Path -Path $targetDir)) {
    New-Item -Path $targetDir -ItemType Directory | Out-Null
    Write-Host "Created target directory: $targetDir" -ForegroundColor Green
}

# Step 1a: Ensure shared-content directory exists
$sharedContentPath = Join-Path $targetDir "Notebooks/shared-content"
if (-Not (Test-Path $sharedContentPath)) {
    New-Item -ItemType Directory -Path $sharedContentPath -Force | Out-Null
    Write-Host "Created missing shared-content directory at $sharedContentPath" -ForegroundColor Green
}

# Step 2: Copy ProjectTemplate folders to target directory
Copy-Item -Path "$scriptDir\ProjectTemplate\*" -Destination $targetDir -Recurse -Force
Write-Host "Copied ProjectTemplate to $targetDir" -ForegroundColor Green

# Step 3: Update docker-compose.yaml environment variables
$dockerComposeFile = Join-Path $targetDir "Sandboxes\code-interpreter\docker-compose.yaml"
if (-not (Test-Path $dockerComposeFile)) {
    Write-Host "docker-compose.yaml not found at $dockerComposeFile. Exiting." -ForegroundColor Red
    exit 1
}
$dockerComposeContent = [IO.File]::ReadAllText($dockerComposeFile)

function Update-OrAddEnvVar {
    param(
        [string]$FileContent,
        [string]$VarName,
        [string]$Value
    )
    $pattern = "- $VarName="
    $startIndex = $FileContent.IndexOf($pattern)
    if ($startIndex -ge 0) {
        $valueStart = $startIndex + $pattern.Length
        $valueEnd = $FileContent.IndexOf("`n", $valueStart)
        if ($valueEnd -lt 0) {
            $valueEnd = $FileContent.Length
        }
        $existingValue = $FileContent.Substring($valueStart, $valueEnd - $valueStart).Trim()
        return $FileContent.Replace("$pattern$existingValue", "$pattern$Value")
    } else {
        return $FileContent.Replace("environment:", "environment:`n    - $VarName=$Value")
    }
}

$dockerComposeContent = Update-OrAddEnvVar $dockerComposeContent "AZURE_OPENAI_RESOURCE" $azureOpenAIResource
$dockerComposeContent = Update-OrAddEnvVar $dockerComposeContent "AZURE_OPENAI_API_KEY" $azureOpenAIApiKey
$dockerComposeContent = Update-OrAddEnvVar $dockerComposeContent "AZURE_OPENAI_DEPLOYMENT" $azureOpenAIDeployment
$dockerComposeContent = Update-OrAddEnvVar $dockerComposeContent "AZURE_OPENAI_API_VERSION" $azureOpenAIApiVersion

[IO.File]::WriteAllText($dockerComposeFile, $dockerComposeContent)
Write-Host "docker-compose.yaml updated." -ForegroundColor Green

# Step 4: Prompt for volume mount change
function Get-VolumePath {
    param(
        [string]$FileContent,
        [string]$Pattern
    )
    $startIndex = $FileContent.IndexOf($Pattern)
    if ($startIndex -lt 0) {
        return ""
    }
    $endIndex = $FileContent.IndexOf(":/app/shared/content", $startIndex)
    if ($endIndex -lt 0) {
        $endIndex = $FileContent.Length
    }
    return $FileContent.Substring($startIndex, $endIndex - $startIndex).Trim()
}

$currentSharedContentPath = Get-VolumePath $dockerComposeContent "../../Notebooks/shared-content"
$newSharedContentPath = Prompt-VolumePath $currentSharedContentPath
$patternToReplace = $currentSharedContentPath + ":/app/shared/content"
$newPattern = $newSharedContentPath + ":/app/shared/content"
$dockerComposeContent = $dockerComposeContent.Replace($patternToReplace, $newPattern)
[IO.File]::WriteAllText($dockerComposeFile, $dockerComposeContent)
Write-Host "Volume path updated." -ForegroundColor Green

# Step 5: Patch JSON config files
function Update-JsonFile {
    param(
        [string]$FilePath,
        [string]$VarName,
        [string]$Value
    )
    $fileContent = [IO.File]::ReadAllText($FilePath)
    $fileContent = $fileContent.Replace($VarName, $Value)
    [IO.File]::WriteAllText($FilePath, $fileContent)
}

$configFilePath = Join-Path $targetDir "Notebooks\config\settings.json"
$kernelMemoryFilePath = Join-Path $targetDir "Sandboxes\code-interpreter\kernel-memory\appsettings.json"

Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_RESOURCE" -Value $azureOpenAIResource
Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_API_KEY" -Value $azureOpenAIApiKey
Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_DEPLOYMENT" -Value $azureOpenAIDeployment
Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_API_VERSION" -Value $azureOpenAIApiVersion

Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_RESOURCE" -Value $azureOpenAIResource
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_API_KEY" -Value $azureOpenAIApiKey
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_DEPLOYMENT" -Value $azureOpenAIDeployment
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_API_VERSION" -Value $azureOpenAIApiVersion

Write-Host "JSON files updated." -ForegroundColor Green

# Step 6: Restart containers
Write-Host "Stopping containers..." -ForegroundColor Yellow
docker compose -f $dockerComposeFile down

Write-Host "Starting containers..." -ForegroundColor Yellow
docker compose -f $dockerComposeFile up -d

Write-Host "Uploading docs to memory..." -ForegroundColor Yellow
Start-Sleep -Seconds 10
.\upload-to-memory.ps1

Write-Host "Deployment completed." -ForegroundColor Green
