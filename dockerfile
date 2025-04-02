# Use the official ASP.NET Core runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Install Docker CLI
RUN apt-get update && apt-get install -y \
    apt-transport-https \
    ca-certificates \
    curl \
    gnupg2 \
    software-properties-common && \
    curl -fsSL https://download.docker.com/linux/debian/gpg | apt-key add - && \
    add-apt-repository \
    "deb [arch=amd64] https://download.docker.com/linux/debian \
    $(lsb_release -cs) \
    stable" && \
    apt-get update && \
    apt-get install -y docker.io

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the AntRunnerLib project files
COPY ["AntRunnerLib/AntRunnerLib.csproj", "AntRunnerLib/"]
COPY ["AntRunnerLib/", "AntRunnerLib/"]

# Copy the AntRunner.ToolCalling project files
COPY ["AntRunner.ToolCalling/AntRunner.ToolCalling.csproj", "AntRunner.ToolCalling/"]
COPY ["AntRunner.ToolCalling/", "AntRunner.ToolCalling/"]

COPY ["AntRunner.Assistants/AntRunner.Assistants.csproj", "AntRunner.Assistants/"]
COPY ["AntRunner.Assistants/", "AntRunner.Assistants/"]

# Copy the main project files
COPY ["AntRunner.Services/AntRunner.Services.csproj", "AntRunner.Services/"]
COPY ["AntRunner.Services/", "AntRunner.Services/"]

WORKDIR /src/AntRunner.Services
RUN dotnet restore "AntRunner.Services.csproj"
RUN dotnet build "AntRunner.Services.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AntRunner.Services.csproj" -c Release -o /app/publish

# Copy the build output to the runtime image
FROM base AS final
EXPOSE 80
EXPOSE 443
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AntRunner.Services.dll"]