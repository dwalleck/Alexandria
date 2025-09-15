using System;
using System.Collections.Generic;

namespace Alexandria.Domain.Errors;

/// <summary>
/// Base record for all parsing errors
/// </summary>
public abstract record ParsingError(string Message);

/// <summary>
/// Error when a file is not found
/// </summary>
public sealed record FileNotFoundError(string FilePath)
    : ParsingError($"File not found: {FilePath}");

/// <summary>
/// Error when the EPUB format is invalid
/// </summary>
public sealed record InvalidFormatError(string FilePath, string Details)
    : ParsingError($"Invalid EPUB format in '{FilePath}': {Details}");

/// <summary>
/// Error when the EPUB structure is invalid
/// </summary>
public sealed record InvalidStructureError(string Component, string Details)
    : ParsingError($"Invalid EPUB structure - {Component}: {Details}");

/// <summary>
/// Error when parsing fails
/// </summary>
public sealed record ParsingFailedError(string Details, Exception? InnerException = null)
    : ParsingError($"Parsing failed: {Details}");

/// <summary>
/// Error for unsupported EPUB version
/// </summary>
public sealed record UnsupportedVersionError(string Version)
    : ParsingError($"Unsupported EPUB version: {Version}");

/// <summary>
/// Error when validation fails
/// </summary>
public sealed record ValidationError(IReadOnlyList<string> Errors)
    : ParsingError($"Validation failed with {Errors.Count} error(s): {string.Join(", ", Errors)}");

/// <summary>
/// Generic unexpected error
/// </summary>
public sealed record UnexpectedError(string Message, Exception? Exception = null)
    : ParsingError(Message);