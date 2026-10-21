using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;

namespace IoTCommTool.App.Services;

/// <summary>轻量界面主题偏好：跟随系统 / 浅色 / 深色，保存在用户配置目录。</summary>
public static class ThemeManager
{
    public const string SystemLabel = "跟随系统";
    public const string LightLabel = "浅色";
    public const string DarkLabel = "深色";

    private sealed class UiSettings
    {
        public string? Theme { get; set; }
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IoTCommTool",
        "ui-settings.json");

    public static IReadOnlyList<string> Modes { get; } = new[] { SystemLabel, LightLabel, DarkLabel };

    public static string CurrentMode { get; private set; } = SystemLabel;

    /// <summary>Raised after the requested theme variant changes.</summary>
    public static event Action? ThemeChanged;

    public static void Initialize()
    {
        CurrentMode = Load();
        Apply(CurrentMode);
    }

    public static void Apply(string? mode)
    {
        CurrentMode = Normalize(mode);
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = CurrentMode switch
        {
            LightLabel => ThemeVariant.Light,
            DarkLabel => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        ThemeChanged?.Invoke();
    }

    public static void Save(string? mode)
    {
        try
        {
            string normalized = Normalize(mode);
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(
                new UiSettings { Theme = normalized },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // A read-only profile should never prevent the theme switch from working in-session.
        }
    }

    private static string Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return SystemLabel;

            UiSettings? settings = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(SettingsPath));
            return Normalize(settings?.Theme);
        }
        catch
        {
            return SystemLabel;
        }
    }

    private static string Normalize(string? mode)
        => mode is LightLabel or DarkLabel or SystemLabel ? mode : SystemLabel;
}
