using Godot;
using System;

public class CustomLogger
{
    public static void print(string _text)
    {
        GD.Print(convertText(_text));
    }

    public static void printError(string _text)
    {
        GD.PrintErr(convertText(_text));
    }

    private static string getTimeStamp()
    {
        return "[" + (Time.GetTicksMsec() * 0.001).ToString("0.00") + "s]";
    }

    private static string convertText(string _text)
    {
        return getTimeStamp() + ": " + _text;
    }
}
