using FluentValidation;

namespace Alexandria.Application.Features.LoadBook;

/// <summary>
/// Validator for LoadBookCommand using FluentValidation
/// </summary>
public sealed class LoadBookValidator : AbstractValidator<LoadBookCommand>
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB
    private static readonly byte[] EpubMagicBytes = { 0x50, 0x4B, 0x03, 0x04 }; // PK.. (ZIP header)

    public LoadBookValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty().WithMessage("File path is required")
            .Must(File.Exists).WithMessage("File does not exist")
            .Must(BeReadableFile).WithMessage("File is not readable")
            .Must(BeValidEpubFile).WithMessage("File is not a valid EPUB file")
            .Must(BeWithinSizeLimit).WithMessage($"File exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)} MB");
    }

    private static bool BeReadableFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return stream.CanRead;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidEpubFile(string filePath)
    {
        try
        {
            // Check file extension
            var extension = Path.GetExtension(filePath);
            if (!string.Equals(extension, ".epub", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check ZIP/EPUB magic bytes (PK signature)
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[4];
            var bytesRead = stream.Read(buffer, 0, 4);

            if (bytesRead < 4)
            {
                return false;
            }

            return buffer.SequenceEqual(EpubMagicBytes);
        }
        catch
        {
            return false;
        }
    }

    private static bool BeWithinSizeLimit(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length <= MaxFileSizeBytes;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Async validator for more complex validation scenarios
/// </summary>
public sealed class LoadBookAsyncValidator : AbstractValidator<LoadBookCommand>
{
    public LoadBookAsyncValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty().WithMessage("File path is required")
            .MustAsync(async (filePath, cancellation) => await FileExistsAsync(filePath, cancellation))
                .WithMessage("File does not exist")
            .MustAsync(async (filePath, cancellation) => await BeValidEpubFileAsync(filePath, cancellation))
                .WithMessage("File is not a valid EPUB file");
    }

    private static async Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken)
    {
        return await Task.Run(() => File.Exists(filePath), cancellationToken);
    }

    private static async Task<bool> BeValidEpubFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            // Check file extension
            var extension = Path.GetExtension(filePath);
            if (!string.Equals(extension, ".epub", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check for required EPUB structure
            await using var stream = File.OpenRead(filePath);
            using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

            // Check for required META-INF/container.xml
            var containerEntry = archive.GetEntry("META-INF/container.xml");
            if (containerEntry == null)
            {
                return false;
            }

            // Check for mimetype file (EPUB requirement)
            var mimetypeEntry = archive.GetEntry("mimetype");
            if (mimetypeEntry != null)
            {
                using var mimetypeStream = mimetypeEntry.Open();
                using var reader = new StreamReader(mimetypeStream);
                var mimetype = await reader.ReadToEndAsync(cancellationToken);
                if (!mimetype.Trim().Equals("application/epub+zip", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}