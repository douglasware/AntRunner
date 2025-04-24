This library uses assembly.GetManifestResourceStream(resourceName) to load embedded assistant definitions.

```
  <ItemGroup>
    <EmbeddedResource Include="AssistantDefinitions\sillypirate\manifest.json">
      <LogicalName>SillyPirate</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
```

It also uses Azure blob storage. If the resourceName is not found it will look there instead... 