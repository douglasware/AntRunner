# PowerShellModule.psm1
$scriptdir = $PSScriptRoot

# Get the module directory path
$modulePath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Get the assemblies folder path
$assembliesPath = Join-Path -Path $modulePath -ChildPath "assemblies"

# Define the assembly paths
$assemblyPaths = @(
    (Join-Path -Path $assembliesPath -ChildPath "OpenAI.dll"),
    (Join-Path -Path $assembliesPath -ChildPath "AntRunnerLib.dll")
)

<#
.SYNOPSIS
    Loads the specified assemblies from the provided paths.

.DESCRIPTION
    The Load-Assemblies function attempts to load the specified .NET assemblies from the provided paths. 
    If an assembly fails to load, detailed error information is provided.

.PARAMETER AssemblyPaths
    An array of paths to the assemblies that need to be loaded.

.EXAMPLE
    $assemblyPaths = @("path\to\OpenAI.dll", "path\to\AntRunnerLib.dll")
    Load-Assemblies -AssemblyPaths $assemblyPaths

    This example loads the OpenAI.dll and AntRunnerLib.dll assemblies.
#>
function Load-Assemblies {
    param (
        [Parameter(Mandatory=$true)]
        [array]$AssemblyPaths
    )

    foreach ($assemblyPath in $AssemblyPaths) {
        if (Test-Path -Path $assemblyPath) {
            try {
                Add-Type -Path $assemblyPath
                Write-Verbose "Successfully loaded assembly: $assemblyPath"
            } catch {
                Write-Warning "Failed to load assembly: $assemblyPath"
                if ($_.Exception.InnerException -and $_.Exception.InnerException.LoaderExceptions) {
                    $_.Exception.InnerException.LoaderExceptions | ForEach-Object {
                        Write-Error $_.Message
                    }
                } else {
                    Write-Error $_.Exception.Message
                }
            }
        } else {
            Write-Warning "Assembly not found: $assemblyPath"
        }
    }
}

<#
.SYNOPSIS
    Checks if a specified assembly is already loaded.

.DESCRIPTION
    The Is-AssemblyLoaded function checks whether a given assembly, by name, 
    is already loaded in the current application domain.

.PARAMETER AssemblyName
    The name of the assembly to check.

.EXAMPLE
    $isLoaded = Is-AssemblyLoaded -AssemblyName "OpenAI"
    if ($isLoaded) { "Assembly is loaded" } else { "Assembly is not loaded" }

    This example checks if the OpenAI assembly is loaded.
#>
function Is-AssemblyLoaded {
    param (
        [Parameter(Mandatory=$true)]
        [string]$AssemblyName
    )

    $loadedAssemblies = [System.AppDomain]::CurrentDomain.GetAssemblies()
    $assembly = $loadedAssemblies | Where-Object { $_.GetName().Name -eq $AssemblyName }
    return $assembly -ne $null
}

# Load the required assemblies
Load-Assemblies -AssemblyPaths $assemblyPaths

if ((Is-AssemblyLoaded -AssemblyName "AntRunnerLib") -and (Is-AssemblyLoaded -AssemblyName "OpenAI")) {
    Write-Verbose "Both AntRunnerLib and OpenAI assemblies are loaded."
} else {
    Write-Warning "One or both of the required assemblies are not loaded."
}

<#
.SYNOPSIS
    Sets the configuration for the AntRunner session.

.DESCRIPTION
    The Set-AntRunnerSessionConfig function sets up the environment variables required 
    for the AntRunner session, including storage connection details and Azure OpenAI settings.

.PARAMETER AssistantsStorageConnection
    The storage connection string for assistants (default: "UseDevelopmentStorage=true").

.PARAMETER AssistantsStorageContainer
    The storage container name for assistants (default: "assistants").

.PARAMETER AzureOpenAIResource
    The resource name for Azure OpenAI

.PARAMETER AzureOpenAIApiKey
    The API key for Azure OpenAI

.PARAMETER AzureOpenAIDeployment
    The deployment ID for Azure OpenAI

.PARAMETER AzureOpenAIApiVersion
    The API version for Azure OpenAI (default: "2024-05-01-preview").

.EXAMPLE
    Set-AntRunnerSessionConfig -AssistantsStorageConnection "UseProdStorage=true" -AzureOpenAIResource "prod-resource"

    This example sets the AntRunner session configuration using the provided parameters.
#>
function Set-AntRunnerSessionConfig {
    param (
        [Parameter(Mandatory=$false)]
        [string]$AssistantsBaseFolderPath = $scriptdir,    
    
        [Parameter(Mandatory=$false)]
        [string]$AssistantsStorageConnection = "UseDevelopmentStorage=true",

        [Parameter(Mandatory=$false)]
        [string]$AssistantsStorageContainer = "assistants",

        [Parameter(Mandatory=$false)]
        [string]$AzureOpenAIResource,

        [Parameter(Mandatory=$false)]
        [string]$AzureOpenAIApiKey,

        [Parameter(Mandatory=$false)]
        [string]$AzureOpenAIDeployment

        [Parameter(Mandatory=$false)]
        [string]$AzureOpenAIApiVersion = "2024-05-01-preview"
    )

    # Set environment variables
    $env:ASSISTANTS_STORAGE_CONNECTION = $AssistantsStorageConnection
    $env:ASSISTANTS_STORAGE_CONTAINER = $AssistantsStorageContainer
    $env:AZURE_OPENAI_RESOURCE = $AzureOpenAIResource
    $env:AZURE_OPENAI_API_KEY = $AzureOpenAIApiKey
    $env:AZURE_OPENAI_DEPLOYMENT = $AzureOpenAIDeployment
    $env:AZURE_OPENAI_API_VERSION = $AzureOpenAIApiVersion
    $env:ASSISTANTS_BASE_FOLDER_PATH = $AssistantsBaseFolderPath

    Write-Output "AntRunner session configuration has been set."
}

<#
.SYNOPSIS
    Retrieves the list of assistants from the configured Azure OpenAI service.

.DESCRIPTION
    The Get-AssistantsList function retrieves a list of assistants from the Azure OpenAI service
    using the configuration set in the environment variables.

.EXAMPLE
    Get-AssistantsList

    This example retrieves and displays the list of assistants from the Azure OpenAI service.
#>
function Get-AssistantsList {
    param ()

    # Check if necessary environment variables are set
    $requiredVars = @(
        "AZURE_OPENAI_RESOURCE",
        "AZURE_OPENAI_API_KEY",
        "AZURE_OPENAI_DEPLOYMENT",
        "AZURE_OPENAI_API_VERSION"
    )

    $missingVars = @()
    foreach ($var in $requiredVars) {
        if ([System.String]::IsNullOrWhiteSpace((Get-ChildItem "Env:$var").Value)) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-Error "The following required environment variables are not set: $($missingVars -join ', ')"
        return
    }

    try {
        # Create an instance of AzureOpenAIConfig
        $azureOpenAIConfig = New-Object -TypeName AntRunnerLib.AzureOpenAIConfig
        $azureOpenAIConfig.ResourceName = (Get-ChildItem "Env:AZURE_OPENAI_RESOURCE").Value
        $azureOpenAIConfig.ApiKey = (Get-ChildItem "Env:AZURE_OPENAI_API_KEY").Value
        $azureOpenAIConfig.ApiVersion = (Get-ChildItem "Env:AZURE_OPENAI_API_VERSION").Value
        $azureOpenAIConfig.DeploymentId = (Get-ChildItem "Env:AZURE_OPENAI_DEPLOYMENT").Value

        # Call the ListAssistants method
        $assistantsList = [AntRunnerLib.AssistantUtility]::ListAssistants($azureOpenAIConfig).GetAwaiter().GetResult()

        if ($assistantsList) {
            $assistantsList | ForEach-Object {
                [PSCustomObject]@{
                    CreatedAt       = $_.CreatedAt
                    Description     = $_.Description
                    Id              = $_.Id
                    Instructions    = $_.Instructions
                    Metadata        = $_.Metadata
                    Model           = $_.Model
                    Name            = $_.Name
                    ResponseFormat  = $_.ResponseFormat
                    Temperature     = $_.Temperature
                    ToolResources   = $_.ToolResources
                    Tools           = $_.Tools
                    TopP            = $_.TopP
                }
            }
        } else {
            Write-Warning "No assistants found or an error occurred while fetching the list."
        }
    } catch {
        Write-Error "An error occurred while fetching the list of assistants. Error: $_"
    }
}

<#
.SYNOPSIS
    Deletes an assistant from the Azure OpenAI service.

.DESCRIPTION
    The Remove-Assistant function deletes a specified assistant from the Azure OpenAI service
    using the provided assistant name and configuration.

.PARAMETER AssistantName
    The name of the assistant to delete.

.EXAMPLE
    Remove-Assistant -AssistantName "TestAssistant"

    This example deletes the assistant named "TestAssistant" from the Azure OpenAI service.
#>
function Remove-Assistant {
    param (
        [Parameter(Mandatory=$true)]
        [string]$AssistantName
    )

    # Check if necessary environment variables are set
    $requiredVars = @(
        "AZURE_OPENAI_RESOURCE",
        "AZURE_OPENAI_API_KEY",
        "AZURE_OPENAI_DEPLOYMENT",
        "AZURE_OPENAI_API_VERSION"
    )

    $missingVars = @()
    foreach ($var in $requiredVars) {
        if ([System.String]::IsNullOrWhiteSpace((Get-ChildItem "Env:$var").Value)) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-Error "The following required environment variables are not set: $($missingVars -join ', ')"
        return
    }

    try {
        # Create an instance of AzureOpenAIConfig
        $azureOpenAIConfig = New-Object -TypeName AntRunnerLib.AzureOpenAIConfig
        $azureOpenAIConfig.ResourceName = (Get-ChildItem "Env:AZURE_OPENAI_RESOURCE").Value
        $azureOpenAIConfig.ApiKey = (Get-ChildItem "Env:AZURE_OPENAI_API_KEY").Value
        $azureOpenAIConfig.ApiVersion = (Get-ChildItem "Env:AZURE_OPENAI_API_VERSION").Value
        $azureOpenAIConfig.DeploymentId = (Get-ChildItem "Env:AZURE_OPENAI_DEPLOYMENT").Value

        # Call the DeleteAssistant method
        $result = [AntRunnerLib.AssistantUtility]::DeleteAssistant($AssistantName, $azureOpenAIConfig).GetAwaiter().GetResult()

        Write-Output "Assistant '$AssistantName' has been deleted successfully."
    } catch {
        Write-Error "An error occurred while deleting the assistant. Error: $_"
    }
}

<#
.SYNOPSIS
    Adds a new assistant to the Azure OpenAI service.

.DESCRIPTION
    The Add-Assistant function creates a new assistant on the Azure OpenAI service
    using the provided assistant name and configuration.

.PARAMETER AssistantName
    The name of the assistant to create.

.EXAMPLE
    Add-Assistant -AssistantName "NewAssistant"

    This example creates a new assistant named "NewAssistant" on the Azure OpenAI service.
#>
function Add-Assistant {
    param (
        [Parameter(Mandatory=$true)]
        [string]$AssistantName
    )

    # Check if necessary environment variables are set
    $requiredVars = @(
        "AZURE_OPENAI_RESOURCE",
        "AZURE_OPENAI_API_KEY",
        "AZURE_OPENAI_DEPLOYMENT",
        "AZURE_OPENAI_API_VERSION"
    )

    $missingVars = @()
    foreach ($var in $requiredVars) {
        if ([System.String]::IsNullOrWhiteSpace((Get-ChildItem "Env:$var").Value)) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-Error "The following required environment variables are not set: $($missingVars -join ', ')"
        return
    }

    try {
        # Create an instance of AzureOpenAIConfig
        $azureOpenAIConfig = New-Object -TypeName AntRunnerLib.AzureOpenAIConfig
        $azureOpenAIConfig.ResourceName = (Get-ChildItem "Env:AZURE_OPENAI_RESOURCE").Value
        $azureOpenAIConfig.ApiKey = (Get-ChildItem "Env:AZURE_OPENAI_API_KEY").Value
        $azureOpenAIConfig.ApiVersion = (Get-ChildItem "Env:AZURE_OPENAI_API_VERSION").Value
        $azureOpenAIConfig.DeploymentId = (Get-ChildItem "Env:AZURE_OPENAI_DEPLOYMENT").Value

        # Call the Create method
        $result = [AntRunnerLib.AssistantUtility]::Create($AssistantName, $azureOpenAIConfig).GetAwaiter().GetResult()

        Write-Output "Assistant '$AssistantName' has been created successfully."
    } catch {
        Write-Error "An error occurred while creating the assistant. Error: $_"
    }
}

<#
.SYNOPSIS
    Invokes an assistant on the Azure OpenAI service.

.DESCRIPTION
    The Invoke-Assistant function runs an assistant on the Azure OpenAI service using the provided parameters.

.PARAMETER AssistantName
    The name of the assistant to run.

.PARAMETER Instructions
    The instructions for the assistant.

.PARAMETER ThreadId
    The thread identifier of a previous assistant run (optional).

.PARAMETER Files
    A list of resource files (optional).

.PARAMETER OAuthClientId
    The OAuth client ID if required for tool calls to resources like MSGraph (optional).

.PARAMETER OAuthTenantId
    The tenant ID of the app (optional).

.PARAMETER OAuthScopes
    Array of permission scope names

.PARAMETER UseConversationEvaluator
    A boolean value indicating whether to use the conversation evaluator (default: $true).

.PARAMETER DownloadFolder
    The folder where files specified in FilePathAnnotation should be downloaded (optional).

.EXAMPLE
    Invoke-Assistant -AssistantName "Web Search Test" -Instructions "Use python to get todays date and then use it to find things to do in Atlanta this weekend" -DownloadFolder "C:\Downloads"

    This example runs the assistant named "Web Search Test" with the provided instructions and downloads any files specified in FilePathAnnotation to the specified download folder.

.EXAMPLE
    $OAuthScopes = @("openid", "profile", "email")
    Invoke-Assistant -AssistantName "Graph Query" -Instructions "Fetch my unread emails" -OAuthClientId "your-client-id" -OAuthTenantId "your-tenant-id" -OAuthScopes $OAuthScopes

    This example runs the (hypothetical) assistant named "Graph Query" with the provided instructions and uses OAuth for authentication with the specified scopes.
#>
function Invoke-Assistant {
    param (
        [Parameter(Mandatory=$true)]
        [string]$AssistantName,

        [Parameter(Mandatory=$true)]
        [string]$Instructions,

        [Parameter(Mandatory=$false)]
        [string]$ThreadId,

        [Parameter(Mandatory=$false)]
        [array]$Files,

        [Parameter(Mandatory=$false)]
        [string]$OAuthClientId,

        [Parameter(Mandatory=$false)]
        [string]$OAuthTenantId,

        [Parameter(Mandatory=$false)]
        [string[]]$OAuthScopes,

        [Parameter(Mandatory=$false)]
        [bool]$UseConversationEvaluator = $true,

        [Parameter(Mandatory=$false)]
        [string]$DownloadFolder
    )

    # Check if necessary environment variables are set
    $requiredVars = @(
        "AZURE_OPENAI_RESOURCE",
        "AZURE_OPENAI_API_KEY",
        "AZURE_OPENAI_DEPLOYMENT",
        "AZURE_OPENAI_API_VERSION"
    )

    $missingVars = @()
    foreach ($var in $requiredVars) {
        if ([System.String]::IsNullOrWhiteSpace((Get-ChildItem "Env:$var").Value)) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-Error "The following required environment variables are not set: $($missingVars -join ', ')"
        return
    }

    try {
        # Create an instance of AzureOpenAIConfig
        $azureOpenAIConfig = New-Object -TypeName AntRunnerLib.AzureOpenAIConfig
        $azureOpenAIConfig.ResourceName = (Get-ChildItem "Env:AZURE_OPENAI_RESOURCE").Value
        $azureOpenAIConfig.ApiKey = (Get-ChildItem "Env:AZURE_OPENAI_API_KEY").Value
        $azureOpenAIConfig.ApiVersion = (Get-ChildItem "Env:AZURE_OPENAI_API_VERSION").Value
        $azureOpenAIConfig.DeploymentId = (Get-ChildItem "Env:AZURE_OPENAI_DEPLOYMENT").Value

        # Initialize OAuth token variable
        $OauthUserAccessToken = $null

        # Check if OAuth parameters are provided
        if ($OAuthClientId -and $OAuthTenantId -and $OAuthScopes) {
            # Get the OAuth token using the OAuthHelper class
            $OauthUserAccessToken = [AntRunnerLib.Identity.OAuthHelper]::GetToken($OAuthClientId, $OAuthTenantId, $OAuthScopes).GetAwaiter().GetResult()
        }

        # Create an instance of AssistantRunOptions
        $assistantRunOptions = New-Object -TypeName AntRunnerLib.AssistantRunOptions
        $assistantRunOptions.AssistantName = $AssistantName
        $assistantRunOptions.Instructions = $Instructions
        $assistantRunOptions.ThreadId = $ThreadId
        $assistantRunOptions.Files = $Files
        $assistantRunOptions.OauthUserAccessToken = $OauthUserAccessToken
        $assistantRunOptions.UseConversationEvaluator = $UseConversationEvaluator

        $result = [AntRunnerLib.AssistantRunner]::RunThread($assistantRunOptions, $azureOpenAIConfig).GetAwaiter().GetResult()

        # Check if DownloadFolder is specified
        if ($DownloadFolder) {
            if (-not (Test-Path -Path $DownloadFolder)) {
                New-Item -ItemType Directory -Path $DownloadFolder | Out-Null
            }

            # Create an instance of CodeInterpreterFiles
            $codeInterpreterFiles = [AntRunnerLib.CodeInterpreterFiles]::new()

            # Download files specified in FilePathAnnotation
            foreach ($annotation in $result.Annotations) {
                if ($annotation.Type -eq "file_path") {
                    $fileId = $annotation.FilePathAnnotation.FileId
                    $fileName = [System.IO.Path]::GetFileName($annotation.Text.Replace("sandbox:/mnt/data/", ""))

                    # Retrieve the file content
                    $fileContentResponse = $codeInterpreterFiles.RetrieveFileContent($fileId, $azureOpenAIConfig).GetAwaiter().GetResult()

                    # Check if there was an error during retrieval
                    if ($fileContentResponse.Error -ne $null) {
                        throw [Exception] "Error retrieving file content: $($fileContentResponse.Error.Message)"
                    }

                    # Save the file content to the specified download folder
                    $filePath = [System.IO.Path]::Combine($DownloadFolder, $fileName)
                    [System.IO.File]::WriteAllBytes($filePath, $fileContentResponse.Content)
                }
            }
        }

        # Return the result as a proper object
        return $result
    } catch {
        Write-Error "An error occurred while running the assistant. Error: $_"
    }
}

<#
.SYNOPSIS
    Retrieves file content and saves it to a specified file path.

.DESCRIPTION
    The Get-AssistantFile function retrieves the content of a file from OpenAI storage using the file ID and saves it to the specified file path.

.PARAMETER FileId
    The ID of the file to retrieve.

.PARAMETER FilePath
    The path where the retrieved file content should be saved.

.EXAMPLE
    Get-AssistantFile -FileId "file-id-123" -FilePath "C:\path\to\save\file.txt"

    This example retrieves the file content with the specified file ID and saves it to the provided file path.
#>
function Get-AssistantFile {
    param (
        [Parameter(Mandatory=$true)]
        [string]$FileId,

        [Parameter(Mandatory=$true)]
        [string]$FilePath
    )

    # Check if necessary environment variables are set
    $requiredVars = @(
        "AZURE_OPENAI_RESOURCE",
        "AZURE_OPENAI_API_KEY",
        "AZURE_OPENAI_DEPLOYMENT",
        "AZURE_OPENAI_API_VERSION"
    )

    $missingVars = @()
    foreach ($var in $requiredVars) {
        if ([System.String]::IsNullOrWhiteSpace((Get-ChildItem "Env:$var").Value)) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-Error "The following required environment variables are not set: $($missingVars -join ', ')"
        return
    }

    try {
        # Create an instance of AzureOpenAIConfig
        $azureOpenAIConfig = New-Object -TypeName AntRunnerLib.AzureOpenAIConfig
        $azureOpenAIConfig.ResourceName = (Get-ChildItem "Env:AZURE_OPENAI_RESOURCE").Value
        $azureOpenAIConfig.ApiKey = (Get-ChildItem "Env:AZURE_OPENAI_API_KEY").Value
        $azureOpenAIConfig.ApiVersion = (Get-ChildItem "Env:AZURE_OPENAI_API_VERSION").Value
        $azureOpenAIConfig.DeploymentId = (Get-ChildItem "Env:AZURE_OPENAI_DEPLOYMENT").Value

        # Create an instance of CodeInterpreterFiles
        $codeInterpreterFiles = [AntRunnerLib.CodeInterpreterFiles]::new()

        # Retrieve the file content
        $fileContentResponse = $codeInterpreterFiles.RetrieveFileContent($FileId, $azureOpenAIConfig).GetAwaiter().GetResult()

        # Check if there was an error during retrieval
        if ($fileContentResponse.Error -ne $null) {
            throw [Exception] "Error retrieving file content: $($fileContentResponse.Error.Message)"
        }

        # Save the file content to the specified file path
        [System.IO.File]::WriteAllBytes($FilePath, $fileContentResponse.Content)

        Write-Output "File content saved to $FilePath"
    } catch {
        Write-Error "An error occurred while retrieving and saving the file content. Error: $_"
    }
}

# Export the cmdlets
Export-ModuleMember -Function Set-AntRunnerSessionConfig, Get-AssistantsList, Remove-Assistant, Add-Assistant, Invoke-Assistant, Get-AssistantFile