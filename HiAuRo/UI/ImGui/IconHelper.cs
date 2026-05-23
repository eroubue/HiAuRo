using System.Numerics;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 图标辅助 — 用 Dalamud 内置 Font Awesome 图标字体渲染文本图标
/// 参考: OmenTools.ImGuiOm.ImGuiOm.DrawIconText
/// </summary>
public static class IconHelper
{
    /// <summary>Font Awesome 5 Free 图标 Unicode 字符</summary>
    public static class Icons
    {
        public const string Play = "\uf04b";
        public const string Stop = "\uf04d";
        public const string Pause = "\uf04c";
        public const string Save = "\uf0c7";
        public const string ChevronDown = "\uf078";
        public const string ChevronUp = "\uf077";
        public const string Close = "\uf00d";
        public const string Settings = "\uf013";
        public const string Refresh = "\uf021";
        public const string Expand = "\uf065";
        public const string Lock = "\uf023";
        public const string Unlock = "\uf09c";
        public const string Search = "\uf002";
        public const string Check = "\uf00c";
        public const string Times = "\uf00d";
        public const string Plus = "\uf067";
        public const string Minus = "\uf068";
        public const string ArrowUp = "\uf062";
        public const string ArrowDown = "\uf063";
        public const string ArrowLeft = "\uf060";
        public const string ArrowRight = "\uf061";
        public const string Folder = "\uf07b";
        public const string File = "\uf15b";
        public const string Download = "\uf019";
        public const string Upload = "\uf093";
        public const string Star = "\uf005";
        public const string Heart = "\uf004";
        public const string User = "\uf007";
        public const string Users = "\uf0c0";
        public const string Trophy = "\uf091";
        public const string Shield = "\uf132";
        public const string Sword = "\uf6e3";      // fa-sword (可能需要 FA Pro)
        public const string Crosshairs = "\uf05b";
        public const string Info = "\uf129";
        public const string Warning = "\uf071";
        public const string Question = "\uf128";
        public const string Eye = "\uf06e";
        public const string EyeSlash = "\uf070";
        public const string Undo = "\uf0e2";
        public const string Redo = "\uf01e";
        public const string Copy = "\uf0c5";
        public const string Paste = "\uf0ea";
        public const string Cut = "\uf0c4";
        public const string Bold = "\uf032";
        public const string Italic = "\uf033";
        public const string Underline = "\uf0cd";
        public const string Link = "\uf0c1";
        public const string Clock = "\uf017";
        public const string Calendar = "\uf133";
        public const string Comment = "\uf075";
        public const string Bell = "\uf0f3";
        public const string Envelope = "\uf0e0";
        public const string Home = "\uf015";
        public const string MapMarker = "\uf041";
        public const string Phone = "\uf095";
        public const string Tag = "\uf02b";
        public const string Bookmark = "\uf02e";
        public const string Flag = "\uf024";
        public const string ThumbsUp = "\uf164";
        public const string ThumbsDown = "\uf165";
        public const string Share = "\uf064";
        public const string Trash = "\uf1f8";
        public const string Edit = "\uf044";
        public const string Wrench = "\uf0ad";
        public const string Bug = "\uf188";
        public const string Code = "\uf121";
        public const string Terminal = "\uf120";
        public const string Database = "\uf1c0";
        public const string Cloud = "\uf0c2";
        public const string Sun = "\uf185";
        public const string Moon = "\uf186";
        public const string Fire = "\uf06d";
        public const string Lightning = "\uf0e7";
        public const string Water = "\uf773";
        public const string Wind = "\uf72e";
        public const string Key = "\uf084";
        public const string Cog = "\uf013";
        public const string PowerOff = "\uf011";
        public const string SignOut = "\uf08b";
        public const string Filter = "\uf0b0";
        public const string Sort = "\uf0dc";
    }

    /// <summary>计算图标文本尺寸</summary>
    public static Vector2 CalcIconSize(string iconChar, float scale = 1f)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.CalcTextSize(iconChar) * scale;
    }

    /// <summary>在指定中心绘制图标文本 (通过 DrawList.AddText)</summary>
    public static void DrawIcon(ImDrawListPtr dl, Vector2 center, string iconChar, uint color, float scale = 1f)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(iconChar) * scale;
        dl.AddText(center - size / 2, color, iconChar);
    }
}
