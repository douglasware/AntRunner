# Define parameters
param (
    [Parameter(Mandatory=$false)]
    [string]$TARGET_DIRECTORY,
    
    [Parameter(Mandatory=$false)]
    [string]$AZURE_OPENAI_RESOURCE,
    
    [Parameter(Mandatory=$false)]
    [string]$AZURE_OPENAI_API_KEY,
    
    [Parameter(Mandatory=$false)]
    [string]$AZURE_OPENAI_DEPLOYMENT
)

Set-StrictMode -Version Latest

# Helper functions
function Load-EnvFile {
    param (
        [string]$FilePath
    )
    $envVars = @{}

    if (-Not (Test-Path $FilePath)) {
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

# Check for PowerShell Core
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Write-Host "Error: This script requires PowerShell Core (version 6 or later). Exiting." -ForegroundColor Red
    exit 1
}

# Define environment variables and their requirements
$envVarDefinitions = @(
    @{
        Name = "TARGET_DIRECTORY"
        Required = $true
        Description = "Directory where container images will be built and deployed"
    },
    @{
        Name = "AZURE_OPENAI_RESOURCE"
        Required = $true
        Description = "Your Azure OpenAI resource name"
    },
    @{
        Name = "AZURE_OPENAI_API_KEY"
        Required = $true
        Description = "Your Azure OpenAI API key"
    },
    @{
        Name = "AZURE_OPENAI_DEPLOYMENT"
        Required = $true
        Description = "Your Azure OpenAI model deployment name"
    }
)

# Initialize environment variables from parameters
$envVars = @{}
foreach ($param in $PSBoundParameters.GetEnumerator()) {
    if ($envVarDefinitions.Name -contains $param.Key) {
        $envVars[$param.Key] = $param.Value
    }
}

# Load .env file if present
$envFilePath = Join-Path $PSScriptRoot ".env"
$fileEnvVars = Load-EnvFile -FilePath $envFilePath

# Merge file values with parameter values (parameters take precedence)
foreach ($key in $fileEnvVars.Keys) {
    if (-not $envVars.ContainsKey($key)) {
        $envVars[$key] = $fileEnvVars[$key]
    }
}

# Check for missing required values
$missingVars = @($envVarDefinitions | Where-Object { 
    $varDef = $_
    -not $envVars.ContainsKey($varDef.Name) -or 
    [string]::IsNullOrWhiteSpace($envVars[$varDef.Name])
})

# If we have all parameters, save and exit
if ($PSBoundParameters.Count -eq 4) {
    if ($missingVars.Count -gt 0) {
        Write-Host "Please provide the values for the following missing keys:" -ForegroundColor Yellow
        foreach ($varDef in $missingVars) {
            $envVars[$varDef.Name] = Prompt-EnvVar -VarName $varDef.Name -CurrentValue $envVars[$varDef.Name] -Description $varDef.Description -Required $varDef.Required
        }
    }
    
    $envContent = @()
    foreach ($varDef in $envVarDefinitions) {
        $value = $envVars[$varDef.Name]
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $envContent += "$($varDef.Name)=$value"
        }
    }
    
    [System.IO.File]::WriteAllLines($envFilePath, $envContent)
    Write-Host ".env file saved successfully at $envFilePath" -ForegroundColor Green
    exit 0
}

# If we get here, we're running standalone
if ($missingVars.Count -gt 0) {
    Write-Host "Please provide the values for the following keys required to build the container images for the AntRunner Chat Project:" -ForegroundColor Yellow
    foreach ($varDef in $missingVars) {
        $envVars[$varDef.Name] = Prompt-EnvVar -VarName $varDef.Name -CurrentValue $envVars[$varDef.Name] -Description $varDef.Description -Required $varDef.Required
    }
} elseif (Test-Path $envFilePath) {
    # All values exist, ask if user wants to make changes
    Write-Host "`nCurrent environment variables:" -ForegroundColor Cyan
    foreach ($varDef in $envVarDefinitions) {
        Write-Host "$($varDef.Name)=$($envVars[$varDef.Name])" -ForegroundColor Cyan
    }
    
    Write-Host "`nWould you like to make any changes? (y/n)" -ForegroundColor Yellow
    $response = Read-Host
    if ($response -eq 'y') {
        # User wants to make changes, prompt for each value
        foreach ($varDef in $envVarDefinitions) {
            $envVars[$varDef.Name] = Prompt-EnvVar -VarName $varDef.Name -CurrentValue $envVars[$varDef.Name] -Description $varDef.Description -Required $varDef.Required
        }
    } else {
        exit 0
    }
}

# Save to .env file
$envContent = @()
foreach ($varDef in $envVarDefinitions) {
    $value = $envVars[$varDef.Name]
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        $envContent += "$($varDef.Name)=$value"
    }
}

[System.IO.File]::WriteAllLines($envFilePath, $envContent)
Write-Host ".env file saved successfully at $envFilePath" -ForegroundColor Green
exit 0
