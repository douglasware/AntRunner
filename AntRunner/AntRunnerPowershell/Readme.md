## Building the assemblies for your host device

Open a terminl for the AntRunnerLib project home directory and run the following command to build the assemblies for the host device into the PowerShell module.
If you are using a 64 bit Windows device, you can skip this step and use the pre-built assemblies in the Assemblies directory.

```
dotnet publish -c Debug -o ..\AntRunnerPowershell\Assemblies\ 
```

## Loading the module
Run **LoadModule.ps1** to load the module into your PowerShell session. This will import the module and make the cmdlets available for use.

## Getting help
Run Get-Help <cmdlet> to get help on a specific cmdlet. For example, **Get-Help Get-AssistantsList**
