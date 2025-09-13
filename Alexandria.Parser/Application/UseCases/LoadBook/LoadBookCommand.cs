namespace Alexandria.Parser.Application.UseCases.LoadBook;

/// <summary>
/// Command to load a book from a file
/// </summary>
public sealed record LoadBookCommand
{
    public LoadBookCommand(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        FilePath = filePath;
    }

    public string FilePath { get; }
}