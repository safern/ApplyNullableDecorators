# ApplyNullableDecorators

This is a .NET Core tool intended to apply nullable decorators on corefx on a per type basis. 

The tool will find the type from the specified project, iterate all of its locations (files that define the type) and rewrite them with nullable annotations based on static analysis.

To run the tool just run: `dotnet run -- [action] [args]` under the `src` directory.

The tool has 2 actions, `apply` and `apistats`. These are the arguments that can be passed per each action:
```
ApplyNullableDecorators.exe apply project type [-msbuild value]
  - project   : CS Project containing type to annotate (string, required)
  - type      : Full type name to apply decorators to (string, required)
  - [msbuild] : Full path of MSBuild instance to use to load the workspace (string, default=)

 ApplyNullableDecorators.exe apistats project jetbrainsfiles [-msbuild value]
  - project        : CS Project containing type to annotate (string, required)
  - jetbrainsfiles : JetBrains files separated by ; (string, required)
  - [msbuild]      : Full path of MSBuild instance to use to load the workspace (string, default=)
```

### apply

Loads the project, finds the type, and based on its analysis, rewrites the files with `?` annotations.

### apistats

Prints a summary of total number of public and internal/private APIs that are in the project.
