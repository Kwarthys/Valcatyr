using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public partial class PlayerSetupManager : GridContainer
{
    public static PlayerSetupManager Instance;
    private List<PlayerFields> setupFields = new();
    [Export]
    private Button addAiButton;
    [Export]
    private Button addPlayerButton;
    [Export]
    private Button startGameButton;
    [Export]
    private Control setupMenuHolder;

    public ColorPreviewHelper colorHelper = new();

    public bool factionNamesLoaded { get; private set; } = false;

    public static void show()
    {
        Instance?.setupMenuHolder.Show();
    }

    public void checkForConflicts()
    {
        bool setupConflict = false;
        Dictionary<PlayerFields, List<int>> colorsTakenByFields = new();
        setupFields.ForEach((fields) => colorsTakenByFields.Add(fields, new()));
        foreach (PlayerFields setup in setupFields)
        {
            foreach (PlayerFields otherFields in setupFields)
            {
                if (otherFields == setup)
                    continue;
                colorsTakenByFields[otherFields].Add(setup.data.colorID); // Each setupFields sends its chosen colors to the others, as they aren't allowed to used it
            }
        }

        foreach (PlayerFields setup in setupFields)
        {
            setup.updateColorPreviews(colorsTakenByFields[setup]);
            if (colorsTakenByFields[setup].Contains(setup.data.colorID))
            {
                // Taken colors contains our own, so we're conflicting with another setupfields
                setupConflict = true;
                setup.errorDisplay.Text = "Conflicting Colors";
            }
            else
                setup.errorDisplay.Text = "";
        }

        // Preventing game start if conflicting setup or not enough / too much player
        startGameButton.Disabled = setupConflict || setupFields.Count < 3 || setupFields.Count > 6;
    }

    public override void _Ready()
    {
        Instance = this;

        if (Parameters.factionNames == null)
            Parameters.factionNamesReceived += updateFactionsDisplay;
        else
            factionNamesLoaded = true;

        colorHelper.setupPreviews(Parameters.colors);

        // Default layout
        _addFields(true);
        _addFields(false);
        _addFields(false);
        _addFields(false);
    }

    private void updateFactionsDisplay(object _o, EventArgs _e) // Both will be null, don't need them
    {
        factionNamesLoaded = true;
        setupFields.ForEach( (fields) => fields.rebuildFactionButton() );
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
        GameManager.Instance.onPlayersSetupReady(data);

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
    public OptionButton factionsButton = null;
    public OptionButton colorButton = null;

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

    public void updateColorPreviews(List<int> _takenIDs)
    {
        for (int i = 0; i < colorButton.ItemCount; ++i)
        {
            if (_takenIDs.Contains(i))
                colorButton.SetItemIcon(i, manager.colorHelper.getDisabledPreviewTexture(i));
            else
                colorButton.SetItemIcon(i, manager.colorHelper.getPreviewTexture(i));
        }
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
        colorButton = new();
        for (int i = 0; i < Parameters.colors.Length; ++i)
        {
            colorButton.AddItem(Parameters.colorNames[i]);
            colorButton.SetItemIcon(i, manager.colorHelper.getPreviewTexture(i));
        }

        colorButton.Selected = manager.getFirstFreeColorID();
        data.colorID = colorButton.Selected;
        colorButton.ItemSelected += (index) => { data.colorID = (int)index; manager.checkForConflicts(); };
        return colorButton;
    }

    public void rebuildFactionButton()
    {
        int buttonIndex = fields.IndexOf(factionsButton);
        fields[buttonIndex] = _buildNewFactionButton();
    }

    private OptionButton _buildNewFactionButton()
    {
        factionsButton = new();

        if (manager.factionNamesLoaded)
        {
            for (int i = 0; i < Parameters.factionNames.Length; ++i)
                factionsButton.AddItem(Parameters.factionNames[i]);
        }
        else
        {
            factionsButton.AddItem("Loading");
        }

        factionsButton.Selected = manager.getFirstFreeFactionID();
        data.factionID = factionsButton.Selected;
        factionsButton.ItemSelected += (index) => data.factionID = (int)index;
        return factionsButton;
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
}

public class ColorPreviewHelper
{
    private List<ImageTexture> previews = new();

    public ImageTexture getPreviewTexture(int _colorIndex)
    {
        return previews[_colorIndex * 2];
    }
    public ImageTexture getDisabledPreviewTexture(int _colorIndex)
    {
        return previews[_colorIndex * 2 + 1];
    }

    public void setupPreviews(Color[] _colors)
    {
        for (int i = 0; i < _colors.Length; ++i)
        {
            previews.Add(_createPreviewFromColor(_colors[i]));
            previews.Add(_createDisabledPreviewFromColor(_colors[i]));
        }
    }

    private static ImageTexture _createPreviewFromColor(Color _color)
    {
        Image img = Image.CreateEmpty(20, 20, false, Image.Format.Rgb8);
        img.Fill(_color);
        img.FillRect(new(0, 0, 20, 2), Colors.Black); // Top border
        img.FillRect(new(0, 2, 2, 18), Colors.Black); // Left border
        img.FillRect(new(18, 2, 2, 18), Colors.Black); // Right Border
        img.FillRect(new(0, 18, 20, 2), Colors.Black); // Bottom border
        return ImageTexture.CreateFromImage(img);
    }

    private static ImageTexture _createDisabledPreviewFromColor(Color _color)
    {
        Image img = Image.CreateEmpty(20, 20, false, Image.Format.Rgb8);
        img.Fill(_color);
        img.FillRect(new(0, 0, 20, 2), Colors.Black); // Top border
        img.FillRect(new(0, 2, 2, 18), Colors.Black); // Left border
        img.FillRect(new(18, 2, 2, 18), Colors.Black); // Right Border
        img.FillRect(new(0, 18, 20, 2), Colors.Black); // Bottom border

        // Diagonals
        for (int i = 0; i < 20; ++i)
        {
            img.SetPixel(i, i, Colors.Black);
            img.SetPixel(i, 20 - 1 - i, Colors.Black);
        }

        return ImageTexture.CreateFromImage(img);
    }
}
