# PowerShell script to deploy ProjectTemplate with optional environment variable overrides and volume mapping updates

Set-StrictMode -Version Latest

# Check for PowerShell Core
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Write-Host "Error: This script requires PowerShell Core (version 6 or later). Exiting." -ForegroundColor Red
    exit 1
}

# Get script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Define environment variable definitions
$envVarDefinitions = @(
    @{
        Name = "TARGET_DIRECTORY"
        Required = $true
        Description = "The target directory for the container deployment"
        DefaultValue = "./ContainerBuild"
    },
    @{
        Name = "AZURE_OPENAI_RESOURCE"
        Required = $true
        Description = "Your Azure OpenAI resource name"
        DefaultValue = ""
    },
    @{
        Name = "AZURE_OPENAI_API_KEY"
        Required = $true
        Description = "Your Azure OpenAI API key"
        DefaultValue = ""
    },
    @{
        Name = "AZURE_OPENAI_DEPLOYMENT"
        Required = $true
        Description = "Your Azure OpenAI model deployment name"
        DefaultValue = ""
    }
)

function Load-EnvFile {
    param (
        [string]$FilePath
    )
    $envVars = @{}

    if (-Not (Test-Path $FilePath)) {
        Write-Host "`nWelcome to the AntRunner Chat Project setup." -ForegroundColor Green
        Write-Host "To get your environment configured, we'll need a few details:" -ForegroundColor Cyan
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
        [string]$CurrentValue,
        [string]$Description,
        [bool]$Required
    )
    $requiredText = if ($Required) { "(required)" } else { "(not required)" }
    Write-Host "`n$VarName $requiredText" -ForegroundColor Cyan
    Write-Host "- $Description" -ForegroundColor Cyan
    
    do {
        if ([string]::IsNullOrWhiteSpace($CurrentValue)) {
            Write-Host "Enter the value for ${VarName}:" -ForegroundColor Yellow
        } else {
            Write-Host "Enter the value for ${VarName} or press enter to accept the current value [$CurrentValue]:" -ForegroundColor Yellow
        }
        $inputValue = Read-Host
        if ([string]::IsNullOrWhiteSpace($inputValue)) {
            $inputValue = $CurrentValue
        }
        if ($Required -and [string]::IsNullOrWhiteSpace($inputValue)) {
            Write-Host "This field is required. Please provide a valid value." -ForegroundColor Red
            continue
        }
        break
    } while ($true)
    
    return $inputValue
}

function Save-EnvFile {
    param(
        [string]$FilePath,
        [hashtable]$Variables
    )
    
    try {
        # Call create-env-file.ps1 with named parameters
        & "$PSScriptRoot/create-env-file.ps1" `
            -TARGET_DIRECTORY $Variables["TARGET_DIRECTORY"] `
            -AZURE_OPENAI_RESOURCE $Variables["AZURE_OPENAI_RESOURCE"] `
            -AZURE_OPENAI_API_KEY $Variables["AZURE_OPENAI_API_KEY"] `
            -AZURE_OPENAI_DEPLOYMENT $Variables["AZURE_OPENAI_DEPLOYMENT"]
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Failed to create .env file. Exiting." -ForegroundColor Red
            exit 1
        }
    }
    catch {
        Write-Host "Error: Failed to create .env file: $_" -ForegroundColor Red
        exit 1
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

# Load .env file if present
$envFilePath = Join-Path $scriptDir ".env"
$envVars = Load-EnvFile -FilePath $envFilePath
$originalEnvVars = $envVars.Clone()

# Always prompt for all required values
foreach ($varDef in $envVarDefinitions) {
    $currentValue = if ($envVars.ContainsKey($varDef.Name) -and -not [string]::IsNullOrWhiteSpace($envVars[$varDef.Name])) { 
        $envVars[$varDef.Name] 
    } else { 
        $varDef.DefaultValue
    }
    $envVars[$varDef.Name] = Prompt-EnvVar -VarName $varDef.Name -CurrentValue $currentValue -Description $varDef.Description -Required $varDef.Required
}

# Validate all required values are now populated
$invalidVars = @($envVarDefinitions | Where-Object { 
    $varDef = $_
    $varDef.Required -and [string]::IsNullOrWhiteSpace($envVars[$varDef.Name])
})
if ($invalidVars.Count -gt 0) {
    Write-Host "The following required variables have invalid values: $($invalidVars.Name -join ', ')" -ForegroundColor Red
    exit 1
}

# Check if any values were changed
$valuesChanged = $false
foreach ($varDef in $envVarDefinitions) {
    $key = $varDef.Name
    if ($originalEnvVars.ContainsKey($key) -and $originalEnvVars[$key] -ne $envVars[$key]) {
        $valuesChanged = $true
        break
    }
}

# If values were changed or no .env file existed, ask about saving
if ($valuesChanged -or -not (Test-Path $envFilePath)) {
    Write-Host "`nWould you like to save these values to .env file for future use? (y/n)"
    $saveResponse = Read-Host
    if ($saveResponse -eq 'y') {
        Save-EnvFile -FilePath $envFilePath -Variables $envVars
    }
}

# Extract final values
$targetDir            = $envVars["TARGET_DIRECTORY"]
$azureOpenAIResource   = $envVars["AZURE_OPENAI_RESOURCE"]
$azureOpenAIApiKey     = $envVars["AZURE_OPENAI_API_KEY"]
$azureOpenAIDeployment = $envVars["AZURE_OPENAI_DEPLOYMENT"]

# Convert to absolute path if relative
if (-not [System.IO.Path]::IsPathRooted($targetDir)) {
    $targetDir = Join-Path $scriptDir $targetDir
    Write-Host "Using relative path. Target directory will be created at: $targetDir" -ForegroundColor Yellow
}

# Create target directory
if (-Not (Test-Path -Path $targetDir)) {
    New-Item -Path $targetDir -ItemType Directory | Out-Null
    Write-Host "Created target directory: $targetDir" -ForegroundColor Green
}

# Create shared-content directory
$sharedContentPath = Join-Path $targetDir "Notebooks/shared-content"
if (-Not (Test-Path $sharedContentPath)) {
    New-Item -ItemType Directory -Path $sharedContentPath -Force | Out-Null
    Write-Host "Created missing shared-content directory at $sharedContentPath" -ForegroundColor Green
}

# Copy ProjectTemplate folders to target directory
Copy-Item -Path "$scriptDir\ProjectTemplate\*" -Destination $targetDir -Recurse -Force
Write-Host "Copied ProjectTemplate to $targetDir" -ForegroundColor Green

# Update docker-compose.yaml environment variables
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

Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_RESOURCE" -Value $azureOpenAIResource
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_API_KEY" -Value $azureOpenAIApiKey
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_DEPLOYMENT" -Value $azureOpenAIDeployment

Write-Host "JSON files updated." -ForegroundColor Green

# Build and start containers
Write-Host "`nPreparing containers..." -ForegroundColor Cyan

# Stop any running containers
Write-Host "Stopping any running containers..." -ForegroundColor Yellow
docker compose -f "$dockerComposeFile" down


Write-Host "Starting containers with updated configuration..."
docker compose -f $dockerComposeFile up -d

Write-Host "Uploading docs to memory"
Start-Sleep -Seconds 10
.\upload-to-memory.ps1

Write-Host "Deployment completed successfully."