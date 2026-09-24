using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SableLatch.Desktop;

// HTML is parsed as data only: no browsing context, script engine, CSS loader or renderer.
internal static class FormattedText
{
    private const int Limit = 100_000;
    private static readonly HashSet<string> Allowed = new("b strong i em u s del span p div br pre code ul ol li table tbody thead tfoot tr td th a h1 h2 h3 h4 h5 h6 blockquote hr sup sub".Split(' '));
    private static readonly HashSet<string> Blocked = new("script style iframe object embed svg math template form input button textarea select img link meta base video audio source canvas".Split(' '));
    internal static string Clean(string html)
    {
        if (html.Length > Limit) throw new InvalidDataException("Formatted text is too large.");
        using var document = new HtmlParser().ParseDocument(html);
        var result = new StringBuilder(); var count = 0;
        void Write(INode node, int depth)
        {
            if (++count > 5000 || depth > 64) throw new InvalidDataException("Formatted text is too complex.");
            if (node is IText text) { result.Append(WebUtility.HtmlEncode(text.Data)); return; }
            if (node is not IElement element || Blocked.Contains(element.LocalName)) return;
            var tag = element.LocalName;
            var keep = element.NamespaceUri == "http://www.w3.org/1999/xhtml" && Allowed.Contains(tag);
            if (keep)
            {
                result.Append('<').Append(tag);
                var style = CleanStyle(element.GetAttribute("style") ?? "");
                if (style.Length > 0) result.Append(" style=\"").Append(WebUtility.HtmlEncode(style)).Append('"');
                if (tag == "a" && Uri.TryCreate(element.GetAttribute("href"), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto")
                    result.Append(" href=\"").Append(WebUtility.HtmlEncode(uri.AbsoluteUri)).Append('"');
                foreach (var name in new[] { "colspan", "rowspan" })
                    if (tag is "td" or "th" && int.TryParse(element.GetAttribute(name), out var value) && value is > 0 and <= 100) result.Append(' ').Append(name).Append("=\"").Append(value).Append('"');
                result.Append('>');
            }
            foreach (var child in element.ChildNodes) Write(child, depth + 1);
            if (keep && tag is not ("br" or "hr")) result.Append("</").Append(tag).Append('>');
        }
        foreach (var child in document.Body!.ChildNodes) Write(child, 0);
        if (result.Length > Limit) throw new InvalidDataException("Formatted text is too large.");
        return result.ToString();
    }
    private static string CleanStyle(string style)
    {
        var output = new StringBuilder();
        foreach (var declaration in style.Split(';'))
        {
            var pair = declaration.Split(':', 2); if (pair.Length != 2) continue;
            var name = pair[0].Trim().ToLowerInvariant(); var value = pair[1].Trim().ToLowerInvariant();
            var valid = name switch
            {
                "font-weight" => Regex.IsMatch(value, "^(normal|bold|bolder|[1-9]00)$"),
                "font-style" => value is "normal" or "italic" or "oblique",
                "text-decoration" => value is "none" or "underline" or "line-through",
                "color" or "background-color" => Regex.IsMatch(value, "^(#[0-9a-f]{3,8}|[a-z]{1,20}|rgba?\\([0-9.,% ]{1,40}\\))$"),
                "font-size" => Regex.IsMatch(value, "^[0-9]{1,3}(\\.[0-9]{1,2})?(pt|px|em|%)$"),
                "font-family" => Regex.IsMatch(value, "^[a-z0-9 ,'-]{1,100}$"),
                "text-align" => value is "left" or "right" or "center" or "justify",
                "white-space" => value is "normal" or "pre" or "pre-wrap",
                "direction" => value is "ltr" or "rtl",
                "border-collapse" => value is "collapse" or "separate",
                "border" => Regex.IsMatch(value, "^[0-9]{1,2}px (solid|dotted|dashed) (#[0-9a-f]{3,8}|[a-z]{1,20})$"),
                "padding" or "margin" or "width" => Regex.IsMatch(value, "^[0-9]{1,4}(px|pt|%)$"),
                _ => false
            };
            if (valid) output.Append(name).Append(':').Append(value).Append(';');
        }
        return output.ToString();
    }
    internal static string ReadClipboardHtml(string clipboardHtml)
    {
        if (clipboardHtml.Length > Limit) throw new InvalidDataException("Formatted text is too large.");
        // CF_HTML offsets are UTF-8 byte positions, not .NET character positions.
        var bytes = Encoding.UTF8.GetBytes(clipboardHtml);
        var start = Regex.Match(clipboardHtml, @"(?:^|[\r\n])StartFragment:[ ]*(\d+)(?:[\r\n]|$)");
        var end = Regex.Match(clipboardHtml, @"(?:^|[\r\n])EndFragment:[ ]*(\d+)(?:[\r\n]|$)");
        if (start.Success && end.Success && int.TryParse(start.Groups[1].Value, out var a) && int.TryParse(end.Groups[1].Value, out var b) && a >= 0 && b >= a && b <= bytes.Length)
        {
            var htmlStart = Regex.Match(clipboardHtml, @"(?:^|[\r\n])StartHTML:[ ]*(\d+)(?:[\r\n]|$)");
            var htmlEnd = Regex.Match(clipboardHtml, @"(?:^|[\r\n])EndHTML:[ ]*(\d+)(?:[\r\n]|$)");
            var utf8 = new UTF8Encoding(false, true);
            if (htmlStart.Success && htmlEnd.Success && int.TryParse(htmlStart.Groups[1].Value, out var hs) && int.TryParse(htmlEnd.Groups[1].Value, out var he) && hs <= a && he >= b && he <= bytes.Length)
            {
                var token = Guid.NewGuid().ToString("N");
                using var context = new HtmlParser().ParseDocument(utf8.GetString(bytes.AsSpan(hs, a - hs)) + "<!--" + token + "start-->" + utf8.GetString(bytes.AsSpan(a, b - a)) + "<!--" + token + "end-->" + utf8.GetString(bytes.AsSpan(b, he - b)));
                INode? begin = null, finish = null; var pending = new Stack<(INode Node, int Depth)>(); pending.Push((context, 0)); var count = 0;
                while (pending.TryPop(out var visit))
                {
                    if (++count > 5000 || visit.Depth > 64) throw new InvalidDataException("Formatted text is too complex.");
                    if (visit.Node is IComment comment) { if (comment.Data == token + "start") begin = comment; if (comment.Data == token + "end") finish = comment; }
                    foreach (var child in visit.Node.ChildNodes) pending.Push((child, visit.Depth + 1));
                }
                if (begin is not null && finish is not null)
                {
                    var range = context.CreateRange(); range.StartAfter(begin); range.EndBefore(finish);
                    INode selected = range.CopyContent();
                    for (var parent = range.CommonAncestor as IElement; parent is not null && parent.LocalName is not ("html" or "body"); parent = parent.ParentElement)
                    { var shell = parent.Clone(false); shell.AppendChild(selected); selected = shell; }
                    var holder = context.CreateElement("div"); holder.AppendChild(selected); return Clean(holder.InnerHtml);
                }
            }
            return Clean(utf8.GetString(bytes.AsSpan(a, b - a)));
        }
        if (clipboardHtml.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid HTML clipboard offsets.");
        return Clean(clipboardHtml);
    }
    internal static string ClipboardHtml(string fragment)
    {
        const string header = "Version:1.0\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\nStartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";
        const string before = "<html><body><!--StartFragment-->";
        const string after = "<!--EndFragment--></body></html>";
        var start = Encoding.UTF8.GetByteCount(string.Format(System.Globalization.CultureInfo.InvariantCulture, header, 0, 0, 0, 0));
        var fragmentStart = start + Encoding.UTF8.GetByteCount(before);
        var fragmentEnd = fragmentStart + Encoding.UTF8.GetByteCount(fragment);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, header, start, fragmentEnd + Encoding.UTF8.GetByteCount(after), fragmentStart, fragmentEnd) + before + fragment + after;
    }
}

public sealed class FormattedClipboardWriter : IClipboardWriter
{
    public string MediaType => "application/x-sable-formatted-text";
    public void Write(DataObject destination, ReadOnlyMemory<byte> bytes)
    {
        var value = Codec.Decode<FormattedTextValue>(bytes.Span.ToArray());
        destination.SetText(value.Text, TextDataFormat.UnicodeText);
        // Imported payloads are untrusted: sanitize again on output.
        destination.SetData(DataFormats.Html, FormattedText.ClipboardHtml(FormattedText.Clean(value.Html)), false);
    }
}
