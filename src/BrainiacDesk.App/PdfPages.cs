using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
namespace BrainiacDesk.App;
public static class PdfPages
{
    public static int[] Parse(string text, int pageCount)
    {
        var result = new List<int>();
        foreach (var item in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = item.Split('-', StringSplitOptions.TrimEntries);
            if (range.Length is < 1 or > 2 || !int.TryParse(range[0], out var first) || first < 1 || first > pageCount) throw new ArgumentException("Invalid page selection. Use 1,3-5.");
            var last = first;
            if (range.Length == 2 && (!int.TryParse(range[1], out last) || last < first || last > pageCount)) throw new ArgumentException("Invalid page range.");
            for (var page = first; page <= last; page++) { if (result.Count >= 10000) throw new ArgumentException("Page selection is too large."); result.Add(page - 1); }
        }
        if (result.Count == 0) throw new ArgumentException("Enter at least one page.");
        return result.ToArray();
    }
    public static void Extract(string input, string output, string selection, CancellationToken token)
    {
        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import); using var document = new PdfDocument();
        foreach (var page in Parse(selection, source.PageCount)) { token.ThrowIfCancellationRequested(); document.AddPage(source.Pages[page]); }
        ConversionEngine.WriteNew(output, stream => document.Save(stream, false), token);
    }
    public static string Metadata(string input)
    {
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        return $"Pages: {document.PageCount}\nTitle: {document.Info.Title}\nAuthor: {document.Info.Author}\nSubject: {document.Info.Subject}\nCreator: {document.Info.Creator}";
    }
}