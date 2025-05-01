# Setup Guide

## Prerequisites

Before proceeding with the setup, ensure that your development environment meets the following prerequisites:

### 1. PowerShell Core

All provided PowerShell scripts require **PowerShell Core (version 6 or later)** to run correctly. Windows PowerShell (version 5.1 or earlier) is not supported.

You can download and install PowerShell Core from the official Microsoft repository:

- https://github.com/PowerShell/PowerShell

Verify your PowerShell version by running:

```powershell
$PSVersionTable.PSVersion
```

Ensure the major version is 6 or higher.

### 2. .NET 8 SDK

This project requires the .NET 8 SDK to build and run the solution. Please install the latest .NET 8 SDK from the official Microsoft website:

- Download link: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

After installation, verify the installation by running the following command in PowerShell:

```powershell
dotnet --version
```

The output should indicate a version starting with `8.`.

### 3. Docker

Docker is required to build and run containerized components of the solution. We recommend installing **Docker Desktop**, which provides an easy-to-use interface and includes Docker Engine, Docker CLI client, Docker Compose, and other tools.

- Docker Desktop download: https://www.docker.com/products/docker-desktop

After installation, verify Docker is running correctly by executing:

```powershell
docker --version
docker-compose --version
```

Both commands should return version information without errors.

> **Note:** Ensure that Docker Desktop is configured to use Linux containers (default) unless your project specifies otherwise.

---

## Build Local Docker Images

This section guides you through building the local Docker images required for the project. You will be prompted to choose between building a CPU-only or a CUDA-enabled Python image.

### Step 1: Run the PowerShell build script

Open PowerShell Core in the root folder of the project and run the following script:

```powershell
./build_local_images.ps1
```

### Step 2: Choose between CPU or CUDA image

When running the script, you will be prompted to select which Python image to build:

- **1) CPU-only**: This version supports running PyTorch on the CPU. It is smaller in size and suitable if you do not have an NVIDIA GPU or do not need GPU acceleration.

- **2) CUDA-enabled**: This version includes support for NVIDIA GPUs using CUDA. It enables faster computation for compatible hardware but results in a significantly larger Docker image.

Choose the option that best fits your hardware and use case.

### Step 3: Script actions

The script will:

- Copy and rename kernel-memory JSON files needed for the project.
- Copy the appropriate `docker-compose.yaml` file based on your choice.
- Build the base image `dotnet-9.0-python-3.11`.
- Build the selected Python image (CPU or CUDA).
- Build the PlantUML image.
- Build the `dotnet-server` image.

After successful completion, all selected images will be built and ready to use.

---

## Deploy Project Template

To deploy the project template, which sets up everything in a new folder and allows selection of a folder containing content accessible to the agents, use the `new-chat-project.ps1` PowerShell script.

Run the script from the root folder in PowerShell Core:

```powershell
./new-chat-project.ps1
```

You will be prompted for:

- The target directory where the `ProjectTemplate` will be copied (e.g., `D:\antrunner-chat-docs`).
- Optionally overriding environment variables in `docker-compose.yaml` such as `AZURE_OPENAI_RESOURCE`, `AZURE_OPENAI_API_KEY`, and `AZURE_OPENAI_DEPLOYMENT`.
- The current volume path and an option to enter a new volume path that points to a folder containing content the AI agents can access.

### Example session:

```powershell

.\new-chat-project.ps1
Enter the target directory where ProjectTemplate will be copied: d:\antrunner-chat-docs
You can override the following environment variables in docker-compose.yaml:
Current value of AZURE_OPENAI_RESOURCE is: your-azure-resource
Enter new value for AZURE_OPENAI_RESOURCE (leave blank to keep current): your-azure-resource
Current value of AZURE_OPENAI_API_KEY is: yourkey
Enter new value for AZURE_OPENAI_API_KEY (leave blank to keep current): your-api-key
Current value of AZURE_OPENAI_DEPLOYMENT is: gpt-4.1-mini
Enter new value for AZURE_OPENAI_DEPLOYMENT (leave blank to keep current):
Current volume path is: ../../Notebooks/shared-content
Enter new volume path (leave blank to keep current): D:\Path\To\Your\ContentFolder
Volume path updated successfully.
JSON files updated successfully.
Stopping existing containers...
Starting containers with updated configuration...
[+] Running 6/6
  Network code-interpreter_default  Created
  Container kernel-memory           Started
  Container qdrant                  Started
  Container plantuml                Started
  Container python-app              Started
  Container dotnet-server           Started
Uploading docs to memory
Deployment completed successfully.

```

Upon completing the script, change directory to the target folder and launch Visual Studio Code. The notebooks are configured and ready to run as a result of the deployment script.

---

## Upload Documentation to Memory

The `upload-to-memory.ps1` script is called automatically during deployment to upload documentation files to the memory service.

---

If you need further assistance or encounter issues, please consult the project documentation or reach out for support.
