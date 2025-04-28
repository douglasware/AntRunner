# PowerShell script to build local Docker images with user selection for CPU or CUDA

Write-Host "Select which Python image to build:"
Write-Host "1) CPU-only"
Write-Host "2) CUDA-enabled"
$choice = Read-Host "Enter choice [1 or 2]"

# Build base image: dotnet-9.0-python-3.11
Write-Host "Building base image: dotnet-9.0-python-3.11"
docker build -t dotnet-9.0-python-3.11 -f Sandboxes/Net9AndPython/net9sdk/dockerfile .

switch ($choice) {
    '1' {
        Write-Host "Building python CPU image: python-3.11-dotnet-9-torch-cpu"
        docker build -t python-3.11-dotnet-9-torch-cpu -f Sandboxes/python311TorchCPU/dockerfile .
    }
    '2' {
        Write-Host "Building python CUDA image: python-3.11-dotnet-9-torch-cuda"
        docker build -t python-3.11-dotnet-9-torch-cuda -f Sandboxes/python311TorchCUDA/dockerfile .
    }
    default {
        Write-Error "Invalid choice. Exiting."
        exit 1
    }
}

if (-Not (Test-Path -Path "./AntRunner.Chat.sln")) {
    Write-Error "AntRunner.Chat.sln not found in current directory. Please run this script from the root folder."
    exit 1
}
Write-Host "Building dotnet-server image"
docker build -t dotnet-server .

Write-Host "All selected images built successfully."
