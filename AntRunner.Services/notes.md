### Building the container

From this folder, run 

`docker build -t dotnet-server .`

### Docker Run Command

```powershell
# Set a password for the new certificate
$certPassword = "SecurePassword123!"

# Create the target directory for the certificate export
$certPath = "$env:USERPROFILE\.aspnet\https"
if (-Not (Test-Path -Path $certPath)) {
    New-Item -ItemType Directory -Path $certPath -Force
}

# Remove the existing HTTPS development certificate
dotnet dev-certs https --clean

# Generate and export the new self-signed certificate
dotnet dev-certs https -ep "$certPath\aspnetapp.pfx" -p $certPassword

# Trust the new certificate
dotnet dev-certs https --trust

# Run the Docker container using the new certificate
docker run -e ASPNETCORE_URLS="http://+:80;https://+:443" -e ASPNETCORE_HTTPS_PORT=443 -p 80:80 -p 443:443 -v ${certPath}:/root/.aspnet/https/ -e ASPNETCORE_Kestrel__Certificates__Default__Password=${certPassword} -e ASPNETCORE_Kestrel__Certificates__Default__Path=/root/.aspnet/https/aspnetapp.pfx dotnet-server
```

### Explanation:
1. **Set a password for the new certificate**:
   ```powershell
   $certPassword = "SecurePassword123!"
   ```
   This sets the password for the new certificate to "SecurePassword123!".

2. **Create the target directory for the certificate export**:
   ```powershell
   $certPath = "$env:USERPROFILE\.aspnet\https"
   if (-Not (Test-Path -Path $certPath)) {
       New-Item -ItemType Directory -Path $certPath -Force
   }
   ```
   This creates the target directory if it does not already exist.

3. **Remove the existing HTTPS development certificate**:
   ```powershell
   dotnet dev-certs https --clean
   ```
   This removes the existing HTTPS development certificate.

4. **Generate and export the new self-signed certificate**:
   ```powershell
   dotnet dev-certs https -ep "$certPath\aspnetapp.pfx" -p $certPassword
   ```
   This generates and exports the new self-signed certificate to the specified path with the given password.

5. **Trust the new certificate**:
   ```powershell
   dotnet dev-certs https --trust
   ```
   This trusts the new certificate on the local machine.

6. **Run the Docker container using the new certificate**:
   ```powershell
   docker run -e ASPNETCORE_URLS="http://+:80;https://+:443" -e ASPNETCORE_HTTPS_PORT=443 -p 80:80 -p 443:443 -v ${certPath}:/root/.aspnet/https/ -e ASPNETCORE_Kestrel__Certificates__Default__Password=${certPassword} -e ASPNETCORE_Kestrel__Certificates__Default__Path=/root/.aspnet/https/aspnetapp.pfx dotnet-server
   ```
   This runs the Docker container with the necessary environment variables and volume mounts to use the new certificate, mapping external port 80 to internal port 80 and external port 443 to internal port 443.

By following these steps and using the provided `docker run` command, your ASP.NET Core application should be correctly configured to use HTTPS in Docker. 