namespace DonBot.Tests.CodeStandards;

/// Guards against the bug pattern fixed in AccountEndpoints.VerifyGw2Key:
///
///     var x = await ctx.Set.FirstOrDefaultAsync(...);  // NoTracking by default
///     x.Property = newValue;                            // mutation lost
///     await ctx.SaveChangesAsync();                     // saves nothing
///
/// DatabaseContext sets QueryTrackingBehavior.NoTracking globally, so any code path
/// that queries via FirstOrDefaultAsync (or SingleOrDefaultAsync, FirstAsync, SingleAsync)
/// and then mutates the result must either call .AsTracking() on the query or use an
/// explicit Update()/Attach() before SaveChanges.
///
/// This test scans the API and bot source for that pattern. It is intentionally
/// conservative: it only flags files where a query result is followed by an assignment
/// to a property of that variable AND a SaveChangesAsync, with no AsTracking, Update,
/// or Attach in the same method body.
public class NoTrackingGuardTests
{
    private static readonly string[] ScanRoots =
    [
        "DonBot.Api",
        "GW2-DonBot/Services",
        "DonBot.Core/Services"
    ];

    private static readonly string[] QueryMethods =
    [
        "FirstOrDefaultAsync",
        "SingleOrDefaultAsync",
        "FirstAsync",
        "SingleAsync"
    ];

    [Fact]
    public void NoQueryThenMutateWithoutTrackingOrExplicitUpdate()
    {
        var repoRoot = FindRepoRoot();
        var offenders = new List<string>();

        foreach (var root in ScanRoots)
        {
            var path = Path.Combine(repoRoot, root);
            if (!Directory.Exists(path)) {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("/bin/") || file.Contains("/obj/") || file.Contains("/Migrations/")) {
                    continue;
                }

                var source = File.ReadAllText(file);
                foreach (var method in ExtractMethods(source))
                {
                    var problem = FindProblem(method.Body);
                    if (problem != null)
                    {
                        offenders.Add($"{Path.GetRelativePath(repoRoot, file)} :: {method.Name} - {problem}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Found query-then-mutate-without-tracking patterns:\n  " + string.Join("\n  ", offenders));
    }

    private static string FindRepoRoot()
    {
        // Tests run from DonBot.Tests/bin/Debug/net10.0; walk up to the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DonBot.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static IEnumerable<(string Name, string Body)> ExtractMethods(string source)
    {
        // Crude method extractor: find `name(...)` followed by a balanced { ... } block.
        // Sufficient for scanning; doesn't handle pathological formatting but our codebase is consistent.
        var i = 0;
        while (i < source.Length)
        {
            var openParen = source.IndexOf('(', i);
            if (openParen < 0) {
                yield break;
            }

            // walk back to find the identifier (last word before paren)
            var identEnd = openParen;
            while (identEnd > 0 && char.IsWhiteSpace(source[identEnd - 1])) {
                identEnd--;
            }
            var identStart = identEnd;
            while (identStart > 0 && (char.IsLetterOrDigit(source[identStart - 1]) || source[identStart - 1] == '_')) {
                identStart--;
            }
            if (identStart == identEnd) { i = openParen + 1; continue; }

            var name = source[identStart..identEnd];

            // walk forward, balance parens, then look for {
            var depth = 0;
            var j = openParen;
            for (; j < source.Length; j++)
            {
                if (source[j] == '(') {
                    depth++;
                }
                else if (source[j] == ')') { depth--; if (depth == 0) { j++; break; } }
            }
            if (depth != 0) { i = openParen + 1; continue; }

            // skip whitespace and `where T : ...` constraints
            while (j < source.Length && (char.IsWhiteSpace(source[j]) || char.IsLetter(source[j]) || source[j] == ',' || source[j] == ':' || source[j] == '<' || source[j] == '>')) {
                j++;
            }

            if (j >= source.Length || source[j] != '{') { i = openParen + 1; continue; }

            var bodyStart = j;
            var braceDepth = 0;
            for (; j < source.Length; j++)
            {
                if (source[j] == '{') {
                    braceDepth++;
                }
                else if (source[j] == '}') { braceDepth--; if (braceDepth == 0) { j++; break; } }
            }
            if (braceDepth != 0) {
                yield break;
            }

            yield return (name, source[bodyStart..j]);
            i = j;
        }
    }

    private static string? FindProblem(string body)
    {
        // Find each `var <name> = await <something>.<QueryMethod>(...)` (ignoring projections,
        // which are the `.Select(...).FirstOrDefaultAsync()` shape - those return anonymous
        // types where mutation isn't an issue).
        foreach (var queryMethod in QueryMethods)
        {
            var idx = 0;
            while ((idx = body.IndexOf(queryMethod, idx, StringComparison.Ordinal)) >= 0)
            {
                idx += queryMethod.Length;

                // walk back to find the assignment target
                var lineStart = body.LastIndexOf('\n', idx);
                if (lineStart < 0) {
                    lineStart = 0;
                }
                var line = body[lineStart..idx];

                // ignore projected queries (Select before the query method on the same chain)
                // crude: walk back further within the statement to find the start
                var stmtStart = body.LastIndexOf(';', idx);
                if (stmtStart < 0) {
                    stmtStart = 0;
                }
                var statement = body[stmtStart..idx];

                if (statement.Contains(".Select(") || statement.Contains(".AsTracking()")) {
                    continue;
                }

                // extract the variable name from `var <name> = await ...`
                var varKeyword = statement.IndexOf("var ", StringComparison.Ordinal);
                if (varKeyword < 0) {
                    continue;
                }
                var nameStart = varKeyword + 4;
                var nameEnd = nameStart;
                while (nameEnd < statement.Length && (char.IsLetterOrDigit(statement[nameEnd]) || statement[nameEnd] == '_')) {
                    nameEnd++;
                }
                if (nameEnd == nameStart) {
                    continue;
                }
                var varName = statement[nameStart..nameEnd];

                // look at the rest of the body for: `<varName>.Property = ...` followed by SaveChangesAsync
                var rest = body[idx..];
                if (!System.Text.RegularExpressions.Regex.IsMatch(rest, $@"\b{System.Text.RegularExpressions.Regex.Escape(varName)}\.[A-Z][A-Za-z0-9_]*\s*=")) {
                    continue;
                }
                if (!rest.Contains("SaveChangesAsync")) {
                    continue;
                }

                // explicit Update/Attach saves us
                if (System.Text.RegularExpressions.Regex.IsMatch(rest, $@"\.(Update|Attach)\(\s*{System.Text.RegularExpressions.Regex.Escape(varName)}\s*\)")) {
                    continue;
                }

                return $"`var {varName} = await ...{queryMethod}(...)` is mutated and saved without AsTracking() or explicit Update/Attach";
            }
        }
        return null;
    }
}
