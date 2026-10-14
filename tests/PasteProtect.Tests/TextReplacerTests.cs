namespace PasteProtect.Tests;

public class TextReplacerTests
{
    private static string Replace(string text, params ReplacementRule[] rules) => new TextReplacer(rules).Replace(text);

    private static ReplacementRule Rule(string find, string replace, bool wholeWord = false, CaseMode caseMode = CaseMode.Preserve) =>
        new() { Find = find, Replace = replace, WholeWord = wholeWord, CaseMode = caseMode };

    [Fact]
    public void Matches_substrings_and_preserves_case_by_default() =>
        Assert.Equal(
            "Contoso, contoso.com, CONTOSO_CLIENT and ContosoClient",
            Replace("Acme, acme.com, ACME_CLIENT and AcmeClient", Rule("acme", "contoso")));

    [Fact]
    public void Whole_word_skips_matches_inside_longer_words() =>
        Assert.Equal("contoso acmeCorp my_acme Contoso.", Replace("acme acmeCorp my_acme Acme.", Rule("acme", "contoso", wholeWord: true)));

    [Fact]
    public void Whole_word_works_with_punctuation_at_the_edges() =>
        Assert.Equal("Rust is not abC++.", Replace("C++ is not abC++.", Rule("C++", "Rust", wholeWord: true)));

    [Fact]
    public void Match_case_only_replaces_exact_capitalization() =>
        Assert.Equal("acme Contoso ACME", Replace("acme Acme ACME", Rule("Acme", "Contoso", caseMode: CaseMode.Match)));

    [Fact]
    public void Ignore_case_inserts_replacement_as_typed() =>
        Assert.Equal("[REDACTED] and [REDACTED]", Replace("acme and ACME", Rule("acme", "[REDACTED]", caseMode: CaseMode.Ignore)));

    [Fact]
    public void Higher_rule_wins_where_matches_overlap() =>
        Assert.Equal(
            "Globex and Contoso",
            Replace("Acme Corporation and Acme", Rule("acme corporation", "globex"), Rule("acme", "contoso")));

    [Fact]
    public void Order_decides_even_when_the_lower_rule_is_longer() =>
        Assert.Equal(
            "Contoso Corporation and Contoso",
            Replace("Acme Corporation and Acme", Rule("acme", "contoso"), Rule("acme corporation", "globex")));

    [Fact]
    public void Higher_rule_wins_even_when_a_lower_rule_matches_earlier_in_the_text() =>
        Assert.Equal("acme ltd", Replace("acme corp", Rule("corp", "ltd"), Rule("acme c", "zz")));

    [Fact]
    public void Lower_rule_still_replaces_matches_that_do_not_overlap() =>
        Assert.Equal(
            "aX Y",
            Replace("abc ab", Rule("bc", "X", caseMode: CaseMode.Ignore), Rule("ab", "Y", caseMode: CaseMode.Ignore)));

    [Fact]
    public void Blocked_match_is_retried_just_past_the_blocked_text() =>
        // "aa" first matches at index 1, overlapping "xa"; it must still find the "aa" at index 2.
        Assert.Equal(
            "[1][2]",
            Replace("xaaa", Rule("xa", "[1]", caseMode: CaseMode.Ignore), Rule("aa", "[2]", caseMode: CaseMode.Ignore)));

    [Fact]
    public void Replacements_are_not_matched_again_by_other_rules() =>
        Assert.Equal("bc", Replace("ab", Rule("a", "b"), Rule("b", "c")));

    [Fact]
    public void Replacement_text_is_literal() =>
        Assert.Equal("costs $1 (really)", Replace("costs X (really)", Rule("x", "$1", caseMode: CaseMode.Ignore)));

    [Fact]
    public void Find_text_is_literal_not_a_regex() =>
        Assert.Equal("a.b ab", Replace("a.b a*b", Rule("a*b", "ab")));

    [Fact]
    public void Disabled_and_empty_rules_are_ignored()
    {
        var disabled = Rule("acme", "contoso");
        disabled.Enabled = false;
        Assert.Equal("acme", Replace("acme", disabled, Rule("", "x")));
    }

    [Fact]
    public void Empty_replacement_removes_the_text() =>
        Assert.Equal("Hello !", Replace("Hello Acme!", Rule("acme", "")));

    [Fact]
    public void Line_breaks_are_preserved() =>
        Assert.Equal("contoso\r\nCONTOSO\n", Replace("acme\r\nACME\n", Rule("acme", "contoso")));
}
