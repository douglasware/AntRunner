using CliWrap;
using CliWrap.Buffered;

namespace TestConsole
{
    public class ConsoleTool
    {
        public static async Task<string> ExecuteCommand(string command, string arguments, CancellationToken cancellationToken = default)
        {
            var result = await Cli.Wrap(command)
                .WithArguments(arguments)
                .ExecuteBufferedAsync(cancellationToken);

            return result.StandardOutput;
        }
    }
}
