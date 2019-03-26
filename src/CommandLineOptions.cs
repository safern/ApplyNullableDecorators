using CommandLine.Attributes;
using CommandLine.Attributes.Advanced;

namespace ApplyNullableDecorators
{
    internal class CommandLineOptions
    {
        public const char FILESEPATOR = ';';

        [ActionArgument]
        public CommandLineActionGroup Action { get; set; }

        [RequiredArgument(0, "project", "CS Project containing type to annotate")]
        [ArgumentGroup(nameof(CommandLineActionGroup.apply))]
        [ArgumentGroup(nameof(CommandLineActionGroup.apistats))]
        public string Project { get; set; }

        [OptionalArgument(null, "msbuild", "Full path of MSBuild instance to use to load the workspace")]
        [ArgumentGroup(nameof(CommandLineActionGroup.apply))]
        [ArgumentGroup(nameof(CommandLineActionGroup.apistats))]
        public string MSBuildInstance { get; set; }

        [OptionalArgument(true, "enablenullable", "Switch to determine if we should add #nullable enable/restore to the file")]
        [ArgumentGroup(nameof(CommandLineActionGroup.apply))]
        public bool EnableNullableInFiles { get; set; }

        [RequiredArgument(1, "type", "Full type name to apply decorators to")]
        [ArgumentGroup(nameof(CommandLineActionGroup.apply))]
        public string Type { get; set; }

        [RequiredArgument(1, "jetbrainsfiles", "JetBrains files separated by ;")]
        [ArgumentGroup(nameof(CommandLineActionGroup.apistats))]
        public string JetBrainsFiles { get; set; }
    }

    internal enum CommandLineActionGroup
    {
        apply,
        apistats
    }
}
