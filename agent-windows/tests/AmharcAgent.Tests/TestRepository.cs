namespace AmharcAgent.Tests;

internal static class TestRepository
{
    public static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var gitEntry = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found from the test output directory.");
    }
}
