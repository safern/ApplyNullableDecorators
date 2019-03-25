# apply-nullable-decorators

This is a .NET Core global tool intended to apply nullable decorators on corefx
on a per type basis.

## Installation

    $ dotnet tool install apply-nullable-decorators -g

## Usage

    usage: apply-nullable-decorators <action> [options]

The tool will find the type from the specified project, iterate all of its
locations (files that define the type) and rewrite them with nullable
annotations based on static analysis.

To run the tool just run: `apply-nullable-decorators [action] [args]` under the
`src` directory.

### apply

Loads the project, finds the type, and based on its analysis, rewrites the files
with `?` annotations.

    usage: apply-nullable-decorators apply <project> <type> [-msbuild value]

    Options:
    <project>           CS Project containing type to annotate (string, required)
    <type>              Full type name to apply decorators to (string, required)
    -msbuild <value>    Full path of MSBuild instance to use to load the
                        workspace (string, default=)

### apistats

Prints a summary of total number of public and internal/private APIs that are in the project.

    usage: apply-nullable-decorators apistats <project> <type> [-msbuild value]

    Options:
    <project>           CS Project containing type to annotate (string, required)
    <jetbrainsfiles>    JetBrains files separated by ; (string, required)
    -msbuild <value>    Full path of MSBuild instance to use to load the
                        workspace (string, default=)
