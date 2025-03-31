# Using PlantUML to Create Diagrams

## Running PlantUML

To run PlantUML from the command line and create diagrams, use the following command:

```bash
plantuml <input-file>
```

Replace `<input-file>` with the path to your PlantUML source file.

### Language Items and Skinparams

To display all keywords, skinparameter keywords, and color names:

```bash
plantuml -language
```

## Generating Web-Viewable Image Formats

### PNG Format

To generate images in PNG format (default):

```bash
plantuml -tpng <input-file>
```

### SVG Format

To generate images in SVG format:

```bash
plantuml -tsvg <input-file>
```

### HTML Format

To generate diagrams in HTML format for class diagrams:

```bash
plantuml -thtml <input-file>
```
