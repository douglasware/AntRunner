#!/bin/bash
set -e

echo "Select which Python image to build:"
echo "1) CPU-only"
echo "2) CUDA-enabled"
read -p "Enter choice [1 or 2]: " choice

# Build base image: dotnet-9.0-python-3.11
echo "Building base image: dotnet-9.0-python-3.11"
docker build -t dotnet-9.0-python-3.11 -f Sandboxes/Net9AndPython/net9sdk/dockerfile .

if [ "$choice" == "1" ]; then
  echo "Building python CPU image: python-3.11-dotnet-9-torch-cpu"
  docker build -t python-3.11-dotnet-9-torch-cpu -f Sandboxes/python311TorchCPU/dockerfile .
elif [ "$choice" == "2" ]; then
  echo "Building python CUDA image: python-3.11-dotnet-9-torch-cuda"
  docker build -t python-3.11-dotnet-9-torch-cuda -f Sandboxes/python311TorchCUDA/dockerfile .
else
  echo "Invalid choice. Exiting."
  exit 1
fi

# Build dotnet-server image from root folder
if [ ! -f "AntRunner.Chat.sln" ]; then
  echo "Error: AntRunner.Chat.sln not found in current directory. Please run this script from the root folder."
  exit 1
fi
 echo "Building dotnet-server image"
docker build -t dotnet-server .

echo "All selected images built successfully."
