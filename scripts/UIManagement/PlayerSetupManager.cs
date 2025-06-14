using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerSetupManager : GridContainer
{
    private List<List<Control>> setupFields = new();

    public void onAddPlayerPressed()
    {
        _addFields(true);
    }

    private void onAddAIPressed()
    {
        _addFields(false);
    }

    private void _addFields(bool _isHuman)
    {
        if (setupFields.Count >= 6)
            return;

        setupFields.Add(new());
        List<Control> currentPlayerFields = setupFields.Last();

        // Create all the fields
        if (_isHuman)
            currentPlayerFields.Add(_buildNewPlayerName());
        else
            currentPlayerFields.Add(_buildNewBotName());

        currentPlayerFields.Add(_buildNewColorButton());
        currentPlayerFields.Add(_buildNewFactionButton());

        if (_isHuman == false)
            currentPlayerFields.Add(_buildNewStyleButton());
        else
            currentPlayerFields.Add(_buildNewSpacer());

        currentPlayerFields.Add(_buildNewDeleteButton());

        // Add field to the hierarchy
        currentPlayerFields.ForEach((item) => AddChild(item));

/*
        if (setupFields.Count >= 6)
        {
            // TODO: Disable buttons, then reenable when removing player
        }
*/
    }

    private Button _buildNewDeleteButton()
    {
        Button button = new();
        button.Text = "X";
        return button;
    }

    private OptionButton _buildNewColorButton()
    {
        OptionButton button = new();
        foreach (string colorName in Parameters.colorNames)
            button.AddItem(colorName);

        return button;
    }

    private OptionButton _buildNewFactionButton()
    {
        OptionButton button = new();
        for (int i = 0; i < 6; ++i)
            button.AddItem("WIP");
        return button;
    }

    private OptionButton _buildNewStyleButton()
    {
        OptionButton button = new();
        for (int i = 0; i < 3; ++i)
            button.AddItem("WIP");
        return button;
    }

    private LineEdit _buildNewPlayerName()
    {
        LineEdit edit = new();
        edit.PlaceholderText = "PlayerName";
        edit.ExpandToTextLength = true;
        edit.MaxLength = 20;
        return edit;
    }

    private RichTextLabel _buildNewBotName()
    {
        RichTextLabel label = new();
        label.Text = "Bot";
        return label;
    }

    private Control _buildNewSpacer()
    {
        return new();
    }
}
