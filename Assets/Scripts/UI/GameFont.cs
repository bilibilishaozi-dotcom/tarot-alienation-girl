using System;
using UnityEngine;

public static class GameFont
{
    private static Font cached;

    public static Font Get()
    {
        if (cached != null)
            return cached;

        string[] installed =
        {
            "Microsoft YaHei",
            "Microsoft YaHei UI",
            "SimHei",
            "SimSun",
            "DengXian",
            "Yu Gothic UI",
            "Meiryo"
        };

        try
        {
            cached = Font.CreateDynamicFontFromOSFont(installed, 32);
        }
        catch (Exception)
        {
            cached = null;
        }

        if (cached == null)
            cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cached;
    }
}
