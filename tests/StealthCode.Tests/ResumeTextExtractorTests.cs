using System.IO.Compression;
using System.Text;
using SkiaSharp;
using StealthCode.Services;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;

namespace StealthCode.Tests;

public sealed class ResumeTextExtractorTests
{

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsUnicodeTextAndDetectsBom(bool utf16)
    {
        var encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        var data = encoding.GetPreamble().Concat(encoding.GetBytes("  Zoë — инженер\r\nC# developer  ")).ToArray();
        using var stream = new MemoryStream(data);
        Assert.Equal("Zoë — инженер\nC# developer", ResumeTextExtractor.Extract("resume.TXT", stream, TestContext.Current.CancellationToken));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void ReadsParagraphsTablesHeadersAndFootersInOrder()
    {
        using var stream = Docx(
            ("word/header1.xml", "<w:p><w:r><w:t>Alex Example</w:t></w:r></w:p>"),
            ("word/document.xml", "<w:p><w:r><w:t>Engineer</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Project</w:t><w:tab/><w:t>2024</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"),
            ("word/footer1.xml", "<w:p><w:r><w:t>Portfolio</w:t></w:r></w:p>"));
        Assert.Equal("Alex Example\nEngineer\nProject\t2024\nPortfolio", ResumeTextExtractor.Extract("resume.docx", stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReadsRepresentativeTextPdf()
    {
        using var stream = new MemoryStream(CreatePdf(true));
        var text = ResumeTextExtractor.Extract("resume.pdf", stream, TestContext.Current.CancellationToken);
        Assert.Contains("Alex Example", text);
        Assert.Contains("Software Engineer", text);
    }

    [Fact]
    public void RejectsPdfWithoutReadableText()
    {
        using var stream = new MemoryStream(CreatePdf(false));
        Assert.Contains("OCR", Assert.Throws<InvalidDataException>(() => ResumeTextExtractor.Extract("scan.pdf", stream, TestContext.Current.CancellationToken)).Message);
    }

    [Fact]
    public void RejectsPasswordProtectedPdfWithActionableError()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "password-protected.pdf"));
        var error = Assert.Throws<InvalidDataException>(() => ResumeTextExtractor.Extract("resume.pdf", stream, TestContext.Current.CancellationToken));
        Assert.Contains("encrypted", error.Message);
        Assert.Contains("paste", error.Message);
    }

    [Theory]
    [InlineData("resume.doc")]
    [InlineData("resume.pdf")]
    [InlineData("resume.docx")]
    [InlineData("resume.txt")]
    public void RejectsUnsupportedCorruptOrEmptyFiles(string filename)
    {
        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => ResumeTextExtractor.Extract(filename, stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RejectsOversizedNonSeekableInput()
    {
        using var stream = new NonSeekableStream(new byte[ResumeTextExtractor.MaxFileBytes + 1]);
        Assert.Contains("10 MiB", Assert.Throws<InvalidDataException>(() => ResumeTextExtractor.Extract("resume.txt", stream, TestContext.Current.CancellationToken)).Message);
    }

    [Fact]
    public void BoundsDocxDecompression()
    {
        using var stream = Docx(("word/document.xml", new string(' ', 21 * 1024 * 1024)));
        Assert.Contains("20 MiB", Assert.Throws<InvalidDataException>(() => ResumeTextExtractor.Extract("resume.docx", stream, TestContext.Current.CancellationToken)).Message);
    }

    [Fact]
    public void ProhibitsXmlEntities()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write("<!DOCTYPE doc [<!ENTITY external SYSTEM 'file:///should-never-be-read'>]><doc>&external;</doc>");
        }

        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => ResumeTextExtractor.Extract("resume.docx", stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HonorsCancellation()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("resume"));
        Assert.Throws<OperationCanceledException>(() => ResumeTextExtractor.Extract("resume.txt", stream, new CancellationToken(true)));
    }

    internal static MemoryStream Docx(params (string Path, string Content)[] parts)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var (path, content) in parts)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write($"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">{content}</w:document>");
            }
        }

        stream.Position = 0;
        return stream;
    }

    internal static byte[] CreatePdf(bool withText)
    {
        using var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        if (withText)
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            page.AddText("Alex Example", 16, new PdfPoint(50, 780), font);
            page.AddText("Software Engineer - Acme - 2021 to 2024", 12, new PdfPoint(50, 750), font);
        }
        else
        {
            using var bitmap = new SKBitmap(60, 40);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.LightGray);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            page.AddPng(png.ToArray(), new PdfRectangle(50, 650, 350, 850));
        }

        return builder.Build();
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}
