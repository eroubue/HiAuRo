using Dalamud.Configuration;

namespace Browsingway;

[Serializable]
internal class Configuration : IPluginConfiguration
{
	public List<InlayConfiguration> Inlays = new();
	public int Version { get; set; } = 0;
}

[Serializable]
internal class InlayConfiguration
{
	public int Framerate = 30;
	public Guid Guid;
	public bool Locked;
	public string Name = null!;
	public string Url = null!;
	public float Zoom = 100f;
	public string CustomCss = "";
	public bool Muted;
	public int Width;
	public int Height;
}
