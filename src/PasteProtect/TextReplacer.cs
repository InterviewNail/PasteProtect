using System.Text;
using System.Text.RegularExpressions;

namespace PasteProtect;

/// <summary>
/// Applies the enabled rules to text. The rules are in priority order: where matches overlap, the rule
/// higher in the list wins. Every rule matches against the original text, so one rule's output is never
/// matched again by another rule.
/// </summary>
internal sealed class TextReplacer
{
    private readonly (ReplacementRule Rule, Regex Regex)[] rules;

    public TextReplacer(IEnumerable<ReplacementRule> rules) =>
        this.rules = rules
            .Where(r => r.Enabled && r.Find.Length > 0)
            .Select(r => (r, new Regex(BuildPattern(r), RegexOptions.CultureInvariant)))
            .ToArray();

    public string Replace(string text)
    {
        if (rules.Length == 0 || text.Length == 0)
            return text;

        // Each rule, highest priority first, claims the stretches of text it matches.
        var claimed = new bool[text.Length];
        var replacements = new List<(int Index, int Length, string Text)>();
        foreach (var (rule, regex) in rules)
        {
            var match = regex.Match(text);
            while (match.Success)
            {
                var span = claimed.AsSpan(match.Index, match.Length);
                if (span.Contains(true))
                {
                    // A higher rule already owns part of this; look again one character further on.
                    match = regex.Match(text, match.Index + 1);
                    continue;
                }
                span.Fill(true);
                replacements.Add((match.Index, match.Length, ReplacementFor(rule, match.Value)));
                match = match.NextMatch();
            }
        }
        if (replacements.Count == 0)
            return text;

        replacements.Sort((a, b) => a.Index.CompareTo(b.Index));
        var result = new StringBuilder(text.Length);
        int position = 0;
        foreach (var (index, length, replacement) in replacements)
        {
            result.Append(text, position, index - position).Append(replacement);
            position = index + length;
        }
        return result.Append(text, position, text.Length - position).ToString();
    }

    private static string ReplacementFor(ReplacementRule rule, string matched) =>
        rule.CaseMode == CaseMode.Preserve ? CasePreserver.Apply(matched, rule.Replace) : rule.Replace;

    private static string BuildPattern(ReplacementRule rule)
    {
        string find = rule.Find;
        string pattern = Regex.Escape(find);
        if (rule.WholeWord)
        {
            // Like \b, only require a boundary on edges that are word characters, so "C++" or "@acme" still work.
            if (IsWordChar(find[0]))
                pattern = @"(?<!\w)" + pattern;
            if (IsWordChar(find[^1]))
                pattern += @"(?!\w)";
        }
        return rule.CaseMode == CaseMode.Match ? pattern : $"(?i:{pattern})";
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
