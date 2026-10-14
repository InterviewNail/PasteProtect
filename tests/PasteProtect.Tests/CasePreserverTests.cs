namespace PasteProtect.Tests;

public class CasePreserverTests
{
    [Theory]
    [InlineData("acme", "contoso", "contoso")]
    [InlineData("acme", "Contoso", "contoso")]
    [InlineData("ACME", "contoso", "CONTOSO")]
    [InlineData("Acme", "contoso", "Contoso")]
    [InlineData("Acme Corp", "contoso ltd", "Contoso Ltd")]
    [InlineData("Acme-Corp", "contoso ltd", "Contoso Ltd")]
    [InlineData("Acme corp", "contoso ltd", "Contoso ltd")]
    [InlineData("AcmeCorp", "contosoLtd", "ContosoLtd")]
    [InlineData("acmeCorp", "ContosoLtd", "contosoLtd")]
    [InlineData("A", "an", "An")]
    [InlineData("a", "An", "an")]
    [InlineData("ACME CORP", "contoso ltd", "CONTOSO LTD")]
    [InlineData("12345", "Six", "Six")]
    [InlineData("Acme", "[redacted]", "[Redacted]")]
    [InlineData("Acme", "", "")]
    public void Copies_capitalization_of_matched_text(string matched, string replacement, string expected) =>
        Assert.Equal(expected, CasePreserver.Apply(matched, replacement));
}
