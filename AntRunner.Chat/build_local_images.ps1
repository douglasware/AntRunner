# PowerShell script to build local Docker images with user selection for CPU or CUDA

# Function to copy and rename kernel-memory JSON files
function Copy-And-Rename-Files {
    $sourcePath = "./SetupFiles/*.json.kernel-memory"
    $destinationPath = "./ProjectTemplate/Sandboxes/code-interpreter/kernel-memory/"

    Write-Host "Reading setup files from $sourcePath and copying to $destinationPath."
    
    Get-ChildItem -Path $sourcePath | ForEach-Object {
        $newFileName = $_.Name -replace "kernel-memory", ""
        $newFilePath = Join-Path -Path $destinationPath -ChildPath $newFileName
        Write-Host "Copying and renaming file: $($_.Name) to $newFileName"
        Copy-Item -Path $_.FullName -Destination $newFilePath
    }
}

# Function to copy the appropriate docker-compose.yaml file
function Copy-DockerComposeFile {
    param (
        [string]$selection
    )
    switch ($selection) {
        '1' {
            $sourceFile = "./SetupFiles/docker-compose.yaml.cpu"
        }
        '2' {
            $sourceFile = "./SetupFiles/docker-compose.yaml.cuda"
        }
        '3' {
            $sourceFile = "./SetupFiles/docker-compose.yaml.marm64"
        }
        default {
            Write-Error "Invalid choice. Exiting."
            exit 1
        }
    }
    $destinationFile = "./ProjectTemplate/Sandboxes/code-interpreter/docker-compose.yaml"
    Write-Host "Copying file: $sourceFile to $destinationFile"
    Copy-Item -Path $sourceFile -Destination $destinationFile
}

# Copy and rename JSON kernel-memory files
Copy-And-Rename-Files

Write-Host "Select which Python image to build:"
Write-Host "1) CPU-only"
Write-Host "2) CUDA-enabled"
Write-Host "3) MacOS Arm64 (M-series) CPU-only"
$choice = Read-Host "Enter choice [1, 2, or 3]"

# Copy the appropriate docker-compose.yaml file based on user choice
Copy-DockerComposeFile -selection $choice

# Build base image: dotnet-9.0-python-3.11
Write-Host "Building base image: dotnet-9.0-python-3.11"
docker build -t dotnet-9.0-python-3.11 -f Sandboxes/Net9AndPython/net9sdk/dockerfile Sandboxes/Net9AndPython/net9sdk

switch ($choice) {
    '1' {
        Write-Host "Building python CPU image: python-3.11-dotnet-9-torch (cpu)"
        docker build -t python-3.11-dotnet-9-torch -f Sandboxes/python311TorchCPU/dockerfile Sandboxes/python311TorchCPU
    }
    '2' {
        Write-Host "Building python CUDA image: python-3.11-dotnet-9-torch (cuda)"
        docker build -t python-3.11-dotnet-9-torch -f Sandboxes/python311TorchCUDA/dockerfile Sandboxes/python311TorchCUDA
    }
     '3' {
        Write-Host "Building python CPU image: python-3.11-dotnet-9-torch (MacOS Arm64)"
        docker build -t python-3.11-dotnet-9-torch -f Sandboxes/python311TorchMARM64/dockerfile Sandboxes/python311TorchMARM64
    }
    default {
        Write-Error "Invalid choice. Exiting."
        exit 1
    }
}

docker build -t plantuml-1.2025.2 -f Sandboxes/PlantUml/dockerfile Sandboxes/PlantUml

if (-Not (Test-Path -Path "./AntRunner.Chat.sln")) {
    Write-Error "AntRunner.Chat.sln not found in current directory. Please run this script from the root folder."
    exit 1
}
Write-Host "Building dotnet-server image"
docker build -t dotnet-server .

Write-Host "All selected images built successfully."