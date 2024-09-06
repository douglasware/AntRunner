namespace AntRunnerLib
{
    /// <summary>
    /// Loads an assembly into the library's AppDomain if it is not already loaded.
    /// </summary>
    public class AntLoader
    {
        /// <summary>
        /// Loads an assembly into the library's AppDomain if it is not already loaded.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to load.</param>
        public void LoadAssembly(string assemblyPath)
        {
            // Load the assembly if it is not already loaded.
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location == assemblyPath))
            {
                AppDomain.CurrentDomain.Load(assemblyPath);
            }
        }
    }
}
