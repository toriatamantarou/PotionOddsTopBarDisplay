using System.Globalization;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Runs;

namespace PotionOddsTopBarMod;

public partial class PotionOddsTopBarDisplay : Node
{
    private HBoxContainer? _container;
    private Label? _label;
    private double _refreshTimer;
    private bool _isHovered;
    private int _lastColorBand = -1;
    private string _tooltipText = string.Empty;
    private string _displayedTooltipText = string.Empty;

    private static readonly Color[] OddsColors =
    [
        new Color(0.52f, 0.28f, 0.70f), // 0%: violet
        new Color(0.27f, 0.31f, 0.78f), // 10%: indigo
        new Color(0.12f, 0.48f, 0.85f), // 20%: blue
        new Color(0.22f, 0.75f, 0.91f), // 30%: cyan
        new Color(0.18f, 0.70f, 0.30f), // 40%: green
        new Color(0.55f, 0.78f, 0.20f), // 50%: yellow-green
        new Color(1.00f, 0.82f, 0.05f), // 60%: yellow
        new Color(1.00f, 0.58f, 0.05f), // 70%: orange
        new Color(1.00f, 0.30f, 0.08f), // 80%: red-orange
        new Color(0.98f, 0.18f, 0.12f), // 90%: warm red
        new Color(0.95f, 0.12f, 0.16f)  // 100%: red
    ];

    private static readonly PropertyInfo? HoverTipTitleProperty = typeof(HoverTip).GetProperty(
        nameof(HoverTip.Title),
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static void Install(Node? root)
    {
        if (root == null)
        {
            return;
        }

        var node = new PotionOddsTopBarDisplay
        {
            Name = nameof(PotionOddsTopBarDisplay)
        };
        root.CallDeferred(Node.MethodName.AddChild, node);
    }

    public override void _Process(double delta)
    {
        _refreshTimer -= delta;
        if (_refreshTimer > 0)
        {
            return;
        }

        _refreshTimer = 0.15;

        Label? label = EnsureLabel();
        if (label == null || _container == null)
        {
            return;
        }

        if (!RunManager.Instance.IsInProgress || NRun.Instance?.GlobalUi?.TopBar?.Timer == null)
        {
            _container.Visible = false;
            return;
        }

        var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState());
        if (player == null)
        {
            _container.Visible = false;
            return;
        }

        float currentValue = player.PlayerOdds.PotionReward.CurrentValue;
        float regularOdds = GetPotionOdds(currentValue, isElite: false);
        float eliteOdds = GetPotionOdds(currentValue, isElite: true);
        string regularText = FormatPercent(regularOdds);
        string tooltipText = BuildTooltipText(regularOdds, eliteOdds);

        label.Text = regularText;
        UpdateLabelColor(label, regularOdds);
        _tooltipText = tooltipText;
        _container.Visible = true;

        if (_isHovered && _displayedTooltipText != _tooltipText)
        {
            ShowHoverTip();
        }
    }

    public override void _ExitTree()
    {
        HideHoverTip();
    }

    private Label? EnsureLabel()
    {
        if (_label != null && IsInstanceValid(_label))
        {
            return _label;
        }

        var timerContainer = NRun.Instance?.GlobalUi?.TopBar?.Timer;
        if (timerContainer == null || !IsInstanceValid(timerContainer))
        {
            return null;
        }

        if (timerContainer.GetParent() is not HBoxContainer rightAlignedStuff)
        {
            return null;
        }

        _container = new HBoxContainer
        {
            Name = "PotionOddsContainer",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = string.Empty,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        _container.AddThemeConstantOverride("separation", 6);
        _container.MouseEntered += OnMouseEntered;
        _container.MouseExited += OnMouseExited;
        _container.VisibilityChanged += OnContainerVisibilityChanged;

        var icon = new TextureRect
        {
            Name = "PotionOddsIcon",
            Texture = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/potion_icon.png"),
            CustomMinimumSize = new Vector2(26, 26),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _label = new Label
        {
            Name = "PotionOddsLabel",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "0%"
        };
        _label.CustomMinimumSize = new Vector2(60, 80);
        _lastColorBand = -1;
        UpdateLabelColor(_label, 0f);
        _label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.12549f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0.0980392f, 0.160784f, 0.188235f, 1f));
        _label.AddThemeConstantOverride("shadow_offset_x", 5);
        _label.AddThemeConstantOverride("shadow_offset_y", 4);
        _label.AddThemeConstantOverride("outline_size", 12);
        _label.AddThemeConstantOverride("shadow_outline_size", 0);
        _label.AddThemeFontSizeOverride("font_size", 28);

        _container.AddChild(icon);
        _container.AddChild(_label);

        rightAlignedStuff.AddChild(_container);
        rightAlignedStuff.MoveChild(_container, timerContainer.GetIndex());
        return _label;
    }

    private void UpdateLabelColor(Label label, float odds)
    {
        int colorBand = Mathf.Clamp(Mathf.FloorToInt(odds * 10f), 0, OddsColors.Length - 1);
        if (colorBand == _lastColorBand)
        {
            return;
        }

        label.AddThemeColorOverride("font_color", OddsColors[colorBand]);
        _lastColorBand = colorBand;
    }

    private void OnMouseEntered()
    {
        _isHovered = true;
        ShowHoverTip();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        HideHoverTip();
    }

    private void OnContainerVisibilityChanged()
    {
        if (_container?.Visible != true)
        {
            _isHovered = false;
            HideHoverTip();
        }
    }

    private void ShowHoverTip()
    {
        if (_container == null || !_container.IsVisibleInTree() || string.IsNullOrEmpty(_tooltipText))
        {
            return;
        }

        NHoverTipSet.Remove(_container);

        HoverTip hoverTip = CreateDescriptionOnlyHoverTip(_tooltipText);
        NHoverTipSet.CreateAndShow(_container, hoverTip)?.SetGlobalPosition(
            _container.GlobalPosition + new Vector2(0f, _container.Size.Y + 20f));
        _displayedTooltipText = _tooltipText;
    }

    private void HideHoverTip()
    {
        if (_container != null && IsInstanceValid(_container))
        {
            NHoverTipSet.Remove(_container);
        }

        _displayedTooltipText = string.Empty;
    }

    private static HoverTip CreateDescriptionOnlyHoverTip(string description)
    {
        var hoverTip = new HoverTip(
            new LocString("static_hover_tips", "POTION_SLOT.title"),
            description);

        if (HoverTipTitleProperty == null)
        {
            return hoverTip;
        }

        object boxedHoverTip = hoverTip;
        HoverTipTitleProperty.SetValue(boxedHoverTip, null);
        return (HoverTip)boxedHoverTip;
    }

    private static float GetPotionOdds(float currentValue, bool isElite)
    {
        float eliteBonus = isElite ? PotionRewardOdds.eliteBonus * 0.5f : 0f;
        return Mathf.Clamp(currentValue + eliteBonus, 0f, 1f);
    }

    private static string FormatPercent(float value)
    {
        float percent = value * 100f;
        return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    private static string BuildTooltipText(float regularOdds, float eliteOdds)
    {
        if (IsJapaneseLocale())
        {
            return "通常: " + FormatPercent(regularOdds) + "\n" +
                   "エリート: " + FormatPercent(eliteOdds);
        }

        return "Regular: " + FormatPercent(regularOdds) + "\n" +
               "Elite: " + FormatPercent(eliteOdds);
    }

    private static bool IsJapaneseLocale()
    {
        return LocManager.Instance.Language == "jpn";
    }
}
