namespace ChatBot.Data;

/// <summary>
/// Locates the shared <c>Data</c> folder (training text + saved checkpoint files) at
/// the solution root, so that <see cref="ChatBot.Cli"/> and <see cref="ChatBot.Api"/>
/// - which run from different working directories and different build output folders -
/// both read and write the exact same physical files. Without this, each project would
/// otherwise end up with its own separate copy of the model, and training via one project
/// wouldn't be visible to the other.
/// </summary>
public static class RepoPaths
{
    private static readonly Lazy<string> LazyDataDirectory = new(FindDataDirectory);

    public static string DataDirectory => LazyDataDirectory.Value;

    private static string FindDataDirectory()
    {
        // Walk up from wherever this assembly is actually running (the build output
        // folder) looking for the solution file, which lives at the repo root right
        // next to the shared Data folder.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.sln*").Any())
            {
                return Path.Combine(directory.FullName, "Data");
            }
            directory = directory.Parent;
        }

        // Fallback for a standalone published build with no solution file alongside it:
        // just keep the Data folder next to the executable.
        return Path.Combine(AppContext.BaseDirectory, "Data");
    }
}
