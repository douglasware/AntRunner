# PowerShell script to deploy ProjectTemplate with optional environment variable overrides and volume mapping updates

Set-StrictMode -Version Latest

# Check for PowerShell Core
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Write-Host "Error: This script requires PowerShell Core (version 6 or later). Exiting."
    exit 1
}

function Prompt-EnvVar {
    param(
        [string]$VarName,
        [string]$CurrentValue
    )
    if ([string]::IsNullOrWhiteSpace($CurrentValue)) {
        Write-Host "Current value of $VarName is: <not set>"
    } else {
        Write-Host "Current value of $VarName is: $CurrentValue"
    }
    $inputValue = Read-Host "Enter new value for $VarName (leave blank to keep current)"
    if ([string]::IsNullOrWhiteSpace($inputValue)) {
        return $CurrentValue
    } else {
        return $inputValue
    }
}

function Prompt-YesNo {
    param(
        [string]$Message
    )
    while ($true) {
        $response = Read-Host "$Message (y/n)"
        switch ($response.ToLower()) {
            'y' { return $true }
            'n' { return $false }
            default { Write-Host "Please answer y or n." }
        }
    }
}

# Function to prompt the user for volume path change
function Prompt-VolumePath {
    param(
        [string]$CurrentPath
    )
    if ([string]::IsNullOrWhiteSpace($CurrentPath)) {
        Write-Host "Current volume path is: <not set>"
    } else {
        Write-Host "Current volume path is: $CurrentPath"
    }
    $inputPath = Read-Host "Enter new volume path (leave blank to keep current)"
    if ([string]::IsNullOrWhiteSpace($inputPath)) {
        return $CurrentPath
    } else {
        return $inputPath
    }
}

# 1. Collect target directory from user
$targetDir = Read-Host "Enter the target directory where ProjectTemplate will be copied"
if ([string]::IsNullOrWhiteSpace($targetDir)) {
    Write-Host "Target directory is required. Exiting."
    exit 1
}

# 2. Copy ProjectTemplate folders to target directory
# Assuming the script is in the same folder as the ProjectTemplate folder
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-Not (Test-Path -Path $targetDir)) {
    New-Item -Path $targetDir -ItemType Directory | Out-Null
}
Copy-Item -Path "$scriptDir\ProjectTemplate\*" -Destination $targetDir -Recurse -Force

# 3. Override environment variables in docker-compose.yaml if requested
$dockerComposeFile = Join-Path $targetDir "Sandboxes\code-interpreter\docker-compose.yaml"
if (-not (Test-Path $dockerComposeFile)) {
    Write-Host "docker-compose.yaml not found at $dockerComposeFile. Exiting."
    exit 1
}

# Read current env values from docker-compose.yaml (if any)
$dockerComposeContent = [IO.File]::ReadAllText($dockerComposeFile)

function Get-EnvValue {
    param(
        [string]$FileContent,
        [string]$VarName
    )
    $pattern = "- $VarName="
    $startIndex = $FileContent.IndexOf($pattern)
    if ($startIndex -lt 0) {
        return ""
    }
    $valueStart = $startIndex + $pattern.Length
    $valueEnd = $FileContent.IndexOf("`n", $valueStart)
    if ($valueEnd -lt 0) {
        $valueEnd = $FileContent.Length
    }
    return $FileContent.Substring($valueStart, $valueEnd - $valueStart).Trim()
}

$currentAzureOpenAIResource = Get-EnvValue $dockerComposeContent "AZURE_OPENAI_RESOURCE"
$currentAzureOpenAIApiKey = Get-EnvValue $dockerComposeContent "AZURE_OPENAI_API_KEY"
$currentAzureOpenAIDeployment = Get-EnvValue $dockerComposeContent "AZURE_OPENAI_DEPLOYMENT"

Write-Host "You can override the following environment variables in docker-compose.yaml:"
$azureOpenAIResource = Prompt-EnvVar "AZURE_OPENAI_RESOURCE" $currentAzureOpenAIResource
$azureOpenAIApiKey = Prompt-EnvVar "AZURE_OPENAI_API_KEY" $currentAzureOpenAIApiKey
$azureOpenAIDeployment = Prompt-EnvVar "AZURE_OPENAI_DEPLOYMENT" $currentAzureOpenAIDeployment

# Update or add environment variables in docker-compose.yaml
function Update-OrAddEnvVar {
    param(
        [string]$FileContent,
        [string]$VarName,
        [string]$Value
    )
    $pattern = "- $VarName="
    $startIndex = $FileContent.IndexOf($pattern)
    if ($startIndex -ge 0) {
        # Find the start of the existing value
        $valueStart = $startIndex + $pattern.Length
        $valueEnd = $FileContent.IndexOf("`n", $valueStart)
        if ($valueEnd -lt 0) {
            $valueEnd = $FileContent.Length
        }
        # Replace the existing value with the new value
        $existingValue = $FileContent.Substring($valueStart, $valueEnd - $valueStart).Trim()
        return $FileContent.Replace("$pattern$existingValue", "$pattern$Value")
    } else {
        # Append the new variable if not found
        return $FileContent.Replace("environment:", "environment:`n    - $VarName=$Value")
    }
}

$dockerComposeContent = Update-OrAddEnvVar -FileContent $dockerComposeContent -VarName "AZURE_OPENAI_RESOURCE" -Value $azureOpenAIResource
$dockerComposeContent = Update-OrAddEnvVar -FileContent $dockerComposeContent -VarName "AZURE_OPENAI_API_KEY" -Value $azureOpenAIApiKey
$dockerComposeContent = Update-OrAddEnvVar -FileContent $dockerComposeContent -VarName "AZURE_OPENAI_DEPLOYMENT" -Value $azureOpenAIDeployment

[IO.File]::WriteAllText($dockerComposeFile, $dockerComposeContent)

# 4. Prompt for volume path change
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

# Update volume path in docker-compose.yaml
$dockerComposeContent = [IO.File]::ReadAllText($dockerComposeFile)
$patternToReplace = $currentSharedContentPath + ":/app/shared/content"
$newPattern = $newSharedContentPath + ":/app/shared/content"
$dockerComposeContent = $dockerComposeContent.Replace($patternToReplace, $newPattern)

[IO.File]::WriteAllText($dockerComposeFile, $dockerComposeContent)

Write-Host "Volume path updated successfully."

# 6. Update additional JSON files with environment variables
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

# Paths to JSON files
$configFilePath = Join-Path $targetDir "Notebooks\config\settings.json"
$kernelMemoryFilePath = Join-Path $targetDir "Sandboxes\code-interpreter\kernel-memory\appsettings.json"

# Update JSON files
Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_RESOURCE" -Value $azureOpenAIResource
Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_API_KEY" -Value $azureOpenAIApiKey
Update-JsonFile -FilePath $configFilePath -VarName "AZURE_OPENAI_DEPLOYMENT" -Value $azureOpenAIDeployment

Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_RESOURCE" -Value $azureOpenAIResource
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_API_KEY" -Value $azureOpenAIApiKey
Update-JsonFile -FilePath $kernelMemoryFilePath -VarName "AZURE_OPENAI_DEPLOYMENT" -Value $azureOpenAIDeployment

Write-Host "JSON files updated successfully."

# 7. Execute docker compose down and up
Write-Host "Stopping existing containers..."
docker compose -f $dockerComposeFile down

Write-Host "Starting containers with updated configuration..."
docker compose -f $dockerComposeFile up -d

Write-Host "Uploading docs to memory"
. .\upload-to-memory.ps1

Write-Host "Deployment completed successfully."