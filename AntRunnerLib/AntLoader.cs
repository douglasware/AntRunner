using System.IO;
using System.Reflection;

namespace AntRunnerLib
{
    /// <summary>
    /// Loads an assembly into the library's AppDomain if it is not already loaded.
    /// </summary>
    public static class AntLoader
    {
        /// <summary>
        /// Loads an assembly into the library's AppDomain if it is not already loaded.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to load.</param>
        public static void LoadAssembly(string assemblyPath)
        {
            // Load the assembly if it is not already loaded.
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location == assemblyPath))
            {
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
            }
        }
    }
}
