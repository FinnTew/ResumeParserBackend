namespace ResumeParserBackend.Util;

using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using iText.Kernel.Pdf; 
using iText.Kernel.Pdf.Canvas.Parser;
using NPOI.HWPF;
using NPOI.HWPF.Extractor;

public class FileReader
{
    public string ReadFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();

        return extension switch
        {
            ".txt" => ReadTextFile(filePath),
            ".pdf" => ReadPdfFile(filePath),
            ".docx" => ReadDocxFile(filePath),
            ".doc" => ReadDocFile(filePath),
            _ => throw new NotSupportedException("Unsupported file type.")
        };
    }

    private static string ReadTextFile(string filePath)
    {
        return File.ReadAllText(filePath, Encoding.UTF8);
    }

    private static string ReadPdfFile(string filePath)
    {
        var text = new StringBuilder();

        using (var reader = new PdfReader(filePath))
        using (var pdfDoc = new PdfDocument(reader))
        {
            for (var i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                text.Append(PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i)));
            }
        }

        return text.ToString();
    }

    private static string ReadDocxFile(string filePath)
    {
        var text = new StringBuilder();

        using (var wordDoc = WordprocessingDocument.Open(filePath, false))
        {
            var body = wordDoc.MainDocumentPart?.Document.Body;
            if (body == null)
            {
                return text.ToString();
            }
            text.Append(body.InnerText);
        }

        return text.ToString();
    }

    private static string ReadDocFile(string filePath)
    {
        var text = new StringBuilder();

        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            var doc = new HWPFDocument(fs);
            var extractor = new WordExtractor(doc);

            foreach (var t in extractor.ParagraphText)
            {
                text.Append(t);
            }
        }

        return text.ToString();
    }
}