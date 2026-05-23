using System.Numerics;
using System.Text;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 代码雨特效 — Matrix 风格绿色字符从顶部向下飘落
/// </summary>
public sealed class MatrixRainEffect
{
    private struct RainDrop
    {
        public float X;
        public float Y;
        public float Speed;
        public float Life;
        public float MaxLife;
        public StringBuilder Chars;
    }

    private readonly RainDrop[] _drops;
    private float _lastWidth;

    // 随机字符池：ASCII 可见字符 + 片假名
    private const string CharSet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        "@#$%&*+=-~<>{}[]|/\\!?" +
        "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ";

    public MatrixRainEffect(int maxDrops = 80)
    {
        _drops = new RainDrop[maxDrops];
        for (var i = 0; i < _drops.Length; i++)
            _drops[i].Chars = new StringBuilder(16);
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        var width = max.X - min.X;
        var colSpacing = 14f;
        var colCount = Math.Max(1, (int)(width / colSpacing));

        // 窗口宽度变化时重新分配列位置
        if (Math.Abs(width - _lastWidth) > colSpacing)
        {
            _lastWidth = width;
            for (var i = 0; i < _drops.Length; i++)
            {
                ref var d = ref _drops[i];
                if (d.Life <= 0f)
                    ResetDrop(ref d, min, max, colCount, colSpacing);
            }
        }

        for (var i = 0; i < _drops.Length; i++)
        {
            ref var drop = ref _drops[i];

            if (drop.Life <= 0f)
            {
                ResetDrop(ref drop, min, max, colCount, colSpacing);
                continue;
            }

            drop.Life -= dt;
            drop.Y += drop.Speed * dt;

            // 随机替换尾部字符（闪烁效果）
            if (drop.Chars.Length > 0 && Random.Shared.NextSingle() < 0.1f)
            {
                var idx = Random.Shared.Next(drop.Chars.Length);
                drop.Chars[idx] = CharSet[Random.Shared.Next(CharSet.Length)];
            }

            if (drop.Y > max.Y || drop.Life <= 0f)
                ResetDrop(ref drop, min, max, colCount, colSpacing);
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var accent = Theme.Colors.AccentBlue;

        for (var i = 0; i < _drops.Length; i++)
        {
            ref var drop = ref _drops[i];
            if (drop.Life <= 0f || drop.Chars.Length == 0) continue;

            var lifeRatio = Math.Max(0f, drop.Life / drop.MaxLife);
            var tailLen = drop.Chars.Length;

            for (var j = 0; j < tailLen; j++)
            {
                var charY = drop.Y - j * 14f;

                float alpha;
                if (j == 0)
                {
                    alpha = 0.95f;
                }
                else
                {
                    alpha = Math.Max(0.08f, 0.6f * (1f - (float)j / tailLen));
                }

                alpha *= Math.Min(1f, lifeRatio * 3f);

                Vector4 charColor;
                if (j == 0)
                {
                    charColor = new Vector4(0.85f, 0.95f, 1f, alpha);
                }
                else
                {
                    charColor = new Vector4(0f, 1f, 0.4f, alpha);
                }

                var pos = new Vector2(drop.X, charY);
                dl.AddText(pos, ImGui.ColorConvertFloat4ToU32(charColor), drop.Chars[j].ToString());
            }
        }

        dl.PopClipRect();
    }

    private void ResetDrop(ref RainDrop drop, Vector2 min, Vector2 max, int colCount, float colSpacing)
    {
        var col = Random.Shared.Next(colCount);
        drop.X = min.X + col * colSpacing + Random.Shared.NextSingle() * 4f;
        drop.Y = min.Y + Random.Shared.NextSingle() * 20f;
        drop.Speed = 60f + Random.Shared.NextSingle() * 60f;
        drop.MaxLife = 2f + Random.Shared.NextSingle() * 3f;
        drop.Life = drop.MaxLife;

        var len = 4 + Random.Shared.Next(10);
        drop.Chars.Clear();
        for (var i = 0; i < len; i++)
            drop.Chars.Append(CharSet[Random.Shared.Next(CharSet.Length)]);
    }
}
