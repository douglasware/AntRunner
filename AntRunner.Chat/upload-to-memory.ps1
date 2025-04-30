function Upload-KernelMemoryFile {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ServiceUrl,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $false)]
        [string]$IndexName,

        [Parameter(Mandatory = $false)]
        [string]$DocumentId,

        [Parameter(Mandatory = $false)]
        [string[]]$Tags
    )

    begin {
        # Initialize environment or variables if needed
    }

    process {
        # Validate parameters
        if (-not (Test-Path -Path $FilePath -PathType Leaf)) {
            Write-Error "$FilePath does not exist or is not a file."
            return
        }

        # Prepare form data
        $Form = @{
            file1 = Get-Item -Path $FilePath
            index = $IndexName
            documentId = $DocumentId
            tags = $Tags -join ' '
        }

        try {
            # Send HTTP request using Invoke-RestMethod
            Invoke-RestMethod -Uri "$ServiceUrl/upload" -Method Post -Form $Form -Verbose
        }
        catch {
            Write-Error "An error occurred while uploading the file: $_"
        }
    }

    end {
        # Cleanup or final actions if needed
    }
}

Upload-KernelMemoryFile -ServiceUrl http://127.0.0.1:9001 -FilePath .\SetupFiles\PlantUML_Language_Reference_Guide_en.pdf -IndexName plantuml -Tags "source:https://pdf.plantuml.net/1.2020.22/PlantUML_Language_Reference_Guide_en.pdf" -DocumentId "PlantUML_Language_Reference_Guide_en.pdf"