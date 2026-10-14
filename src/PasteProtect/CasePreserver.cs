namespace PasteProtect;

/// <summary>
/// Gives a replacement the same capitalization as the text it replaces:
/// acme → contoso, ACME → CONTOSO, Acme → Contoso, Acme Corp → Contoso Ltd, acmeCorp → contosoLtd.
/// </summary>
internal static class CasePreserver
{
    public static string Apply(string matched, string replacement)
    {
        if (replacement.Length == 0)
            return replacement;

        int upper = 0, lower = 0;
        foreach (char c in matched)
        {
            if (char.IsUpper(c)) upper++;
            else if (char.IsLower(c)) lower++;
        }

        if (upper == 0 && lower == 0)
            return replacement;                                  // no letters, nothing to copy
        if (lower == 0 && upper > 1)
            return replacement.ToUpperInvariant();               // ACME
        if (upper == 0)
            return replacement.ToLowerInvariant();               // acme
        if (IsTitleCase(matched, out int words) && words > 1)
            return CapitalizeWords(replacement);                 // Acme Corp

        // Acme, AcmeCorp, "A" → capital first letter; acmeCorp → lowercase first letter.
        int first = IndexOfLetter(matched);
        return char.IsUpper(matched[first])
            ? SetFirstLetter(replacement, char.ToUpperInvariant)
            : SetFirstLetter(replacement, char.ToLowerInvariant);
    }

    /// <summary>Every word starts with a capital and has no other capitals.</summary>
    private static bool IsTitleCase(string text, out int words)
    {
        words = 0;
        bool inWord = false, seenLetter = false;
        foreach (char c in text)
        {
            if (!char.IsLetterOrDigit(c))
            {
                inWord = false;
                continue;
            }
            if (!inWord)
            {
                inWord = true;
                seenLetter = false;
                words++;
            }
            if (!char.IsLetter(c))
                continue;
            if (seenLetter ? char.IsUpper(c) : !char.IsUpper(c))
                return false;
            seenLetter = true;
        }
        return true;
    }

    private static string CapitalizeWords(string text)
    {
        var chars = text.ToCharArray();
        bool atWordStart = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                atWordStart = true;
                continue;
            }
            if (atWordStart)
                chars[i] = char.ToUpperInvariant(chars[i]);
            atWordStart = false;
        }
        return new string(chars);
    }

    private static string SetFirstLetter(string text, Func<char, char> convert)
    {
        int i = IndexOfLetter(text);
        if (i < 0)
            return text;
        var chars = text.ToCharArray();
        chars[i] = convert(chars[i]);
        return new string(chars);
    }

    private static int IndexOfLetter(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsLetter(text[i]))
                return i;
        }
        return -1;
    }
}
