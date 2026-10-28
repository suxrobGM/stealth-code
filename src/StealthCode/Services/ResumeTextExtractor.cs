using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace StealthCode.Services;

/// <summary>Local, bounded text extraction. Does not retain files or run a model.</summary>
public static class ResumeTextExtractor
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    private const int MaxExpandedBytes = 20 * 1024 * 1024;
    private const int MaxTextCharacters = 1_000_000;
    private const string FileTooLarge = "The resume exceeds 10 MiB. Choose a smaller file or paste relevant text.";
    private const string DocxTooLarge = "The DOCX expands beyond 20 MiB. Export a smaller document or paste the text.";
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static string Extract(string filename, Stream stream, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (extension is not (".pdf" or ".docx" or ".txt"))
        {
            throw new InvalidDataException("Choose a PDF, DOCX, or TXT resume, or paste your background.");
        }

        using var buffer = ReadBounded(stream, MaxFileBytes, FileTooLarge, cancellationToken);
        string text;
        try
        {
            text = extension switch
            {
                ".txt" => ReadText(buffer),
                ".docx" => ReadDocx(buffer, cancellationToken),
                _ => ReadPdf(buffer, cancellationToken)
            };
        }
        catch (Exception ex) when (ex is not (InvalidDataException or OperationCanceledException or OutOfMemoryException))
        {
            throw new InvalidDataException("Could not read this resume. It may be corrupt or encrypted. Export an unprotected PDF, DOCX, or TXT, or paste the text.", ex);
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("No readable text was found. Scanned PDFs need OCR; paste the text or choose a text-based document.");
        }

        return text;
    }

    /// <summary>Copies at most <paramref name="maxBytes"/> into memory, throwing <paramref name="error"/> beyond that.</summary>
    private static MemoryStream ReadBounded(Stream stream, long maxBytes, string error, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int count;
        while ((count = stream.Read(bytes, 0, bytes.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length + count > maxBytes)
            {
                buffer.Dispose();
                throw new InvalidDataException(error);
            }

            buffer.Write(bytes, 0, count);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static string ReadText(Stream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: true);
        var text = reader.ReadToEnd();
        CheckTextLength(text.Length);
        return text;
    }

    private static string ReadPdf(Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(stream);
        var text = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageText = ContentOrderTextExtractor.GetText(page);
            CheckTextLength((long)text.Length + pageText.Length + 1);
            text.AppendLine(pageText);
        }

        return text.ToString();
    }

    private static string ReadDocx(Stream stream, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var body = archive.GetEntry("word/document.xml")
                   ?? throw new InvalidDataException("This is not a readable DOCX document. Export it again or paste the text.");
        var parts = archive.Entries.Where(e => IsPart(e.FullName, "header")).OrderBy(e => e.FullName)
            .Append(body)
            .Concat(archive.Entries.Where(e => IsPart(e.FullName, "footer")).OrderBy(e => e.FullName));
        var text = new StringBuilder();
        long remainingBytes = MaxExpandedBytes;
        foreach (var part in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var partStream = part.Open();
            using var boundedPart = ReadBounded(partStream, remainingBytes, DocxTooLarge, cancellationToken);
            remainingBytes -= boundedPart.Length;

            using var reader = XmlReader.Create(boundedPart, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxExpandedBytes
            });
            var document = XDocument.Load(reader);
            foreach (var paragraph in document.Descendants(Word + "p"))
            {
                foreach (var run in paragraph.Descendants())
                {
                    var value = run.Name.Namespace != Word ? "" : run.Name.LocalName switch
                    {
                        "t" => run.Value,
                        "tab" => "\t",
                        "br" or "cr" => "\n",
                        _ => ""
                    };
                    CheckTextLength((long)text.Length + value.Length + 1);
                    text.Append(value);
                }

                text.AppendLine();
            }
        }

        return text.ToString();
    }

    private static bool IsPart(string path, string prefix)
    {
        return path.StartsWith("word/" + prefix, StringComparison.Ordinal)
               && path.EndsWith(".xml", StringComparison.Ordinal)
               && !path[5..].Contains('/');
    }

    private static void CheckTextLength(long length)
    {
        if (length > MaxTextCharacters)
        {
            throw new InvalidDataException("The extracted resume is too large. Choose a shorter document or paste relevant text.");
        }
    }
}
