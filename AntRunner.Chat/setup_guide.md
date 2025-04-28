# Setup Guide

## Prerequisites

Before proceeding with the setup, ensure that your development environment meets the following prerequisites:

### 1. .NET 8 SDK

This project requires the .NET 8 SDK to build and run the solution. Please install the latest .NET 8 SDK from the official Microsoft website:

- Download link: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

After installation, verify the installation by running the following command in your terminal or command prompt:

```bash
dotnet --version
```

The output should indicate a version starting with `8.`.

### 2. Docker

Docker is required to build and run containerized components of the solution. We recommend installing **Docker Desktop**, which provides an easy-to-use interface and includes Docker Engine, Docker CLI client, Docker Compose, and other tools.

- Docker Desktop download: https://www.docker.com/products/docker-desktop

After installation, verify Docker is running correctly by executing:

```bash
docker --version
docker-compose --version
```

Both commands should return version information without errors.

> **Note:** Ensure that Docker Desktop is configured to use Linux containers (default) unless your project specifies otherwise.

---

## Build Local Docker Images

This section guides you through building the local Docker images required for the project. You will be prompted to choose between building a CPU-only or a CUDA-enabled Python image.

### Step 1: Make the build script executable (Linux/macOS)

If you are using the provided Bash script from GitHub, you need to make it executable before running it:

```bash
chmod +x build_local_images.sh
```

### Step 2: Run the build script

Run the script appropriate for your environment:

- On Linux/macOS:

  ```bash
  ./build_local_images.sh
  ```

- On Windows PowerShell:

  ```powershell
  ./build_local_images.ps1
  ```

### Step 3: Choose between CPU or CUDA image

When running the script, you will be prompted to select which Python image to build:

- **CPU-only**: This version supports running PyTorch on the CPU. It is smaller in size and suitable if you do not have an NVIDIA GPU or do not need GPU acceleration.

- **CUDA-enabled**: This version includes support for NVIDIA GPUs using CUDA. It enables faster computation for compatible hardware but results in a significantly larger Docker image.

Choose the option that best fits your hardware and use case.

### Additional Notes

- The build process also builds a base image with .NET 9 SDK and Python 3.11, which is used as the foundation for the Python images.

- Finally, the `dotnet-server` image is built from the root folder Dockerfile.
