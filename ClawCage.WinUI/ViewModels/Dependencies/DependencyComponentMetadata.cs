namespace ClawCage.WinUI.ViewModels
{
    internal sealed record DependencyComponentMetadata(
        string Kind,
        string Name,
        string Description,
        string IconGlyph,
        string? IconResourceUri,
        string TargetVersionText,
        bool UseTargetVersionSelector);
}
