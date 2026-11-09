namespace BrainiacDesk.Core;
public sealed record UserSettings
{
    public string Theme { get; init; } = "Dark";
    public bool MinimizeToTray { get; init; } = true;
    public bool ConfirmExit { get; init; } = true;
    public bool OnboardingComplete { get; init; }
}
public interface ISettingsStore
{
    Task<UserSettings> LoadAsync();
    Task SaveAsync(UserSettings settings);
}