using WebSearchFunctions;

namespace RefactorTests
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Environment.SetEnvironmentVariable("SEARCH_API_KEY", "c2a2d83097054852a4db140c8c637092");
            var x = await SearchTool.Search("things to do in Atlanta on September 28, 2024");
            Console.WriteLine(x);
        }
    }
}
