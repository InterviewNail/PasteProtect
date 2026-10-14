using System.Windows.Forms;

namespace PasteProtect.Tests;

public class HotkeyTests
{
    [Fact]
    public void Default_is_ctrl_shift_alt_v() =>
        Assert.Equal("Ctrl+Shift+Alt+V", Hotkey.Default.ToString());

    [Theory]
    [InlineData("Ctrl+Shift+Alt+V")]
    [InlineData("Win+Shift+V")]
    [InlineData("Ctrl+Alt+1")]
    [InlineData("Alt+F12")]
    public void Round_trips_through_text(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(text, hotkey.ToString());
    }

    [Fact]
    public void Parsing_ignores_case_and_spacing()
    {
        Assert.True(Hotkey.TryParse(" control + shift+ALT +v ", out var hotkey));
        Assert.Equal(Hotkey.Default, hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("V")]
    [InlineData("Shift+V")]       // just typing a capital V
    [InlineData("Ctrl+Shift")]    // no key
    [InlineData("Ctrl+V+B")]      // two keys
    [InlineData("Ctrl+86")]       // numbers aren't key names
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+KeyCode")]  // a Keys mask, not a key
    public void Rejects_invalid_shortcuts(string text) =>
        Assert.False(Hotkey.TryParse(text, out _));

    [Fact]
    public void Ctrl_alt_digit_uses_digit_key()
    {
        Assert.True(Hotkey.TryParse("Ctrl+Alt+1", out var hotkey));
        Assert.Equal(Keys.D1, hotkey.Key);
    }
}
