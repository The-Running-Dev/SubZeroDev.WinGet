namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// Derives the library's public surface directly from repository source, per S13.1/S13.5 — no
/// compiled-metadata reflection, so the baseline it is compared against is reviewable as plain
/// text. A file is scanned only when its first top-level type is declared <c>public</c>; a file
/// whose top-level type is <c>internal</c> (the mapper, the COM owner/factory/activation
/// selector) contributes nothing, regardless of what its own members are marked.
/// </summary>
internal static class PublicSurfaceScanner
{
    private static readonly string[] TypeKeywords = ["class", "record", "struct", "interface", "enum"];

    internal static List<string> Scan(string libraryRoot)
    {
        var entries = new List<string>();

        foreach (var file in Directory.EnumerateFiles(libraryRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(libraryRoot, file).Replace('\\', '/');

            if (relative.Split('/').Any(segment => segment is "bin" or "obj" or "buildTransitive"))
            {
                continue;
            }

            ScanFile(relative, File.ReadAllLines(file), entries);
        }

        return entries;
    }

    private static void ScanFile(string relativePath, string[] lines, List<string> entries)
    {
        var depth = 0;
        string? currentKind = null;
        var i = 0;

        while (i < lines.Length)
        {
            var trimmed = lines[i].Trim();

            if (IsBlankOrComment(trimmed))
            {
                i++;
                continue;
            }

            if (depth == 0)
            {
                if (TryMatchTypeKeyword(trimmed, out var kind))
                {
                    var (headerText, consumed) = ConsumeBalancedParens(lines, i, trimmed);
                    var isPublic = trimmed.StartsWith("public ", StringComparison.Ordinal);
                    if (isPublic)
                    {
                        entries.Add($"{relativePath}|{headerText}");
                    }
                    // A non-public top-level type (internal, or unmarked) contributes nothing,
                    // and its body must not be scanned as if it belonged to the enclosing public
                    // type - "skip" marks the body so a public-looking member inside an internal
                    // type is never mistaken for public surface.
                    currentKind = isPublic ? kind : "skip";
                    depth += BraceDelta(string.Join(' ', lines.Skip(i).Take(consumed).Select(l => l.Trim())));
                    i += consumed;
                    continue;
                }

                depth += BraceDelta(trimmed);
                i++;
                continue;
            }

            if (depth == 1)
            {
                if (trimmed == "}")
                {
                    depth = 0;
                    currentKind = null;
                    i++;
                    continue;
                }

                switch (currentKind)
                {
                    case "skip":
                        break;

                    case "enum":
                        foreach (var value in trimmed.TrimEnd(',').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        {
                            entries.Add($"{relativePath}|enum value {value}");
                        }
                        break;

                    case "interface":
                        entries.Add($"{relativePath}|{trimmed}");
                        break;

                    default:
                        if (trimmed.StartsWith("public ", StringComparison.Ordinal))
                        {
                            var (memberText, consumed) = ConsumeBalancedParens(lines, i, trimmed);
                            entries.Add($"{relativePath}|{memberText}");
                            depth += BraceDelta(string.Join(' ', lines.Skip(i).Take(consumed).Select(l => l.Trim())));
                            i += consumed;
                            continue;
                        }
                        break;
                }

                depth += BraceDelta(trimmed);
                i++;
                continue;
            }

            // Inside a member body (depth > 1): not surface, just track nesting.
            depth += BraceDelta(trimmed);
            i++;
        }
    }

    private static bool IsBlankOrComment(string trimmed) =>
        trimmed.Length == 0 ||
        trimmed.StartsWith("//", StringComparison.Ordinal) ||
        trimmed.StartsWith("*", StringComparison.Ordinal) ||
        trimmed.StartsWith("/*", StringComparison.Ordinal);

    private static bool TryMatchTypeKeyword(string trimmed, out string kind)
    {
        foreach (var keyword in TypeKeywords)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, $@"(^|\s){keyword}(\s|\(|$)"))
            {
                kind = keyword;
                return true;
            }
        }

        kind = "";
        return false;
    }

    // Joins continuation lines while a line's opening parens outnumber its closing parens, so a
    // positional record header (or, in principle, a member signature) split across lines becomes
    // one baseline entry instead of silently losing everything after the first line break.
    private static (string text, int linesConsumed) ConsumeBalancedParens(string[] lines, int startIndex, string firstTrimmed)
    {
        var open = firstTrimmed.Count(c => c == '(');
        var close = firstTrimmed.Count(c => c == ')');

        if (open <= close)
        {
            return (firstTrimmed, 1);
        }

        var parts = new List<string> { firstTrimmed };
        var i = startIndex + 1;

        while (open > close && i < lines.Length)
        {
            var next = lines[i].Trim();
            parts.Add(next);
            open += next.Count(c => c == '(');
            close += next.Count(c => c == ')');
            i++;
        }

        return (string.Join(' ', parts), i - startIndex);
    }

    private static int BraceDelta(string text) => text.Count(c => c == '{') - text.Count(c => c == '}');
}
