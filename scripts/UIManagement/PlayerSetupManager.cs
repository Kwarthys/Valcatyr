using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public partial class PlayerSetupManager : GridContainer
{
    private List<PlayerFields> setupFields = new();
    [Export]
    private Button addAiButton;
    [Export]
    private Button addPlayerButton;
    [Export]
    private Button startGameButton;
    [Export]
    private Control setupMenuHolder;

    public void checkForConflicts()
    {
        bool setupConflict = false;
        Dictionary<int, List<PlayerFields>> fieldsPerColorID = new();
        foreach (PlayerFields setup in setupFields)
        {
            setup.errorDisplay.Text = "";
            if (fieldsPerColorID.ContainsKey(setup.data.colorID) == false)
                fieldsPerColorID.Add(setup.data.colorID, new());
            fieldsPerColorID[setup.data.colorID].Add(setup);
        }

        foreach (int colorID in fieldsPerColorID.Keys)
        {
            if (fieldsPerColorID[colorID].Count > 1)
            {
                // Conflict
                setupConflict = true;
                fieldsPerColorID[colorID].ForEach((setup) => setup.errorDisplay.Text = "Conflicting Color choice");
            }
        }

        // Preventing game start if conflicting setup or not enough / too much player
        startGameButton.Disabled = setupConflict || setupFields.Count < 3 || setupFields.Count > 6;
    }

    public override void _Ready()
    {
        // Default layout
        _addFields(true);
        _addFields(false);
        _addFields(false);
        _addFields(false);
    }

    public void onAddPlayerPressed()
    {
        _addFields(true);
    }

    private void onAddAIPressed()
    {
        _addFields(false);
    }

    public void onStartGamePressed()
    {
        List<PlayerData> data = new();
        setupFields.ForEach((fields) => data.Add(fields.data));
        GameManager.Instance.onGameSetupReady(data);

        setupMenuHolder.Visible = false;
    }

    private void _addFields(bool _isHuman)
    {
        if (setupFields.Count >= 6)
            return;

        setupFields.Add(new(_isHuman, this));
        setupFields.Last().fields.ForEach((item) => AddChild(item));

        if (setupFields.Count >= 6)
            setButtonDisabled(true);

        checkForConflicts();
    }

    public void deleteFields(PlayerFields _playerFields)
    {
        _playerFields.fields.ForEach((item) => item.QueueFree());
        setupFields.Remove(_playerFields);

        checkForConflicts();
        setButtonDisabled(setupFields.Count >= 6);
    }

    public int getFirstFreeColorID()
    {
        List<int> colorsTaken = new();
        setupFields.ForEach((setup) => colorsTaken.Add(setup.data.colorID));
        return _getFirstFree(colorsTaken);
    }

    public int getFirstFreeFactionID()
    {
        List<int> factionsTaken = new();
        setupFields.ForEach((setup) => factionsTaken.Add(setup.data.factionID));
        return _getFirstFree(factionsTaken);
    }

    private int _getFirstFree(List<int> _taken)
    {
        for (int i = 0; i < 6; ++i)
        {
            if (_taken.Contains(i) == false)
                return i;
        }
        return -1; // should never happen, 6 possibilities and max 6 players
    }

    private void setButtonDisabled(bool _status)
    {
        addAiButton.Disabled = _status;
        addPlayerButton.Disabled = _status;
    }
}

public class PlayerData
{
    public PlayerData(bool _isHuman) { isHuman = _isHuman; }
    public bool isHuman;
    public int colorID = 0;
    public int factionID = 0;
    public int styleID = 0;
    public string playerName = "";
}

public class PlayerFields
{
    public List<Control> fields = new();

    public PlayerData data;
    public RichTextLabel errorDisplay = null;

    private PlayerSetupManager manager;

    public PlayerFields(bool _isHuman, PlayerSetupManager _manager)
    {
        data = new(_isHuman);
        manager = _manager;

        fields.Add(_buildNewDeleteButton());
        if (_isHuman)
            fields.Add(_buildNewPlayerName());
        else
            fields.Add(_buildNewBotName());

        fields.Add(_buildNewColorButton());
        fields.Add(_buildNewFactionButton());

        if (_isHuman == false)
            fields.Add(_buildNewStyleButton());
        else
            fields.Add(_buildNewSpacer());

        fields.Add(_buildErrorMessageDisplay());
    }

    private Button _buildNewDeleteButton()
    {
        Button button = new();
        button.Text = "X";
        button.AddThemeColorOverride("font_color", Colors.Red);
        button.Pressed += () => manager.deleteFields(this);
        return button;
    }

    private OptionButton _buildNewColorButton()
    {
        OptionButton button = new();
        for (int i = 0; i < Parameters.colors.Length; ++i)
        {
            button.AddItem(Parameters.colorNames[i]);
            button.SetItemIcon(i, _buildColorPreview(Parameters.colors[i]));
        }

        button.Selected = manager.getFirstFreeColorID();
        data.colorID = button.Selected;
        button.ItemSelected += (index) => { data.colorID = (int)index; manager.checkForConflicts(); };
        return button;
    }

    private OptionButton _buildNewFactionButton()
    {
        OptionButton button = new();
        for (int i = 0; i < 6; ++i)
            button.AddItem("WIP_" + i);
        button.Selected = manager.getFirstFreeFactionID();
        data.factionID = button.Selected;
        button.ItemSelected += (index) => data.factionID = (int)index;
        return button;
    }

    private OptionButton _buildNewStyleButton()
    {
        OptionButton button = new();
        button.AddItem("WIP Balanced");
        button.AddItem("WIP Aggressive");
        button.AddItem("WIP Defensive");
        button.ItemSelected += (index) => data.styleID = (int)index;
        return button;
    }

    private LineEdit _buildNewPlayerName()
    {
        LineEdit edit = new();
        edit.PlaceholderText = "PlayerName";
        edit.ExpandToTextLength = true;
        edit.MaxLength = 20;
        edit.TextChanged += (text) => data.playerName = text;
        return edit;
    }

    private RichTextLabel _buildNewBotName()
    {
        RichTextLabel label = new();
        label.Text = "Bot";
        label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        label.FitContent = true;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        return label;
    }

    private RichTextLabel _buildErrorMessageDisplay()
    {
        errorDisplay = new();
        errorDisplay.Text = "";
        errorDisplay.FitContent = true;
        errorDisplay.AutowrapMode = TextServer.AutowrapMode.Off;
        errorDisplay.AddThemeColorOverride("default_color", Colors.Red);
        errorDisplay.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return errorDisplay;
    }

    private Control _buildNewSpacer()
    {
        return new();
    }

    private ImageTexture _buildColorPreview(Color _color)
    {
        Image img = Image.CreateEmpty(20, 20, false, Image.Format.Rgb8);
        img.Fill(_color);
        img.FillRect(new(0, 0, 20, 2), Colors.Black); // Top border
        img.FillRect(new(0, 2, 2, 18), Colors.Black); // Left border
        img.FillRect(new(18, 2, 2, 18), Colors.Black); // Right Border
        img.FillRect(new(0, 18, 20, 2), Colors.Black); // Bottom border
        return ImageTexture.CreateFromImage(img);
    }
}
