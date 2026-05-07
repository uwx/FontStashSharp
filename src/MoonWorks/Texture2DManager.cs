using System;
using System.Drawing;
using System.Runtime.InteropServices;
using FontStashSharp.Interfaces;
using MoonWorks.Graphics;

namespace FontStashSharp.MoonWorks;

/// <summary>
/// Manages texture creation and data upload for FontStashSharp glyph atlases using MoonWorks.
/// </summary>
public static class Texture2DManager
{
	public static Texture CreateTexture(GraphicsDevice device, int width, int height)
	{
		return Texture.Create2D(
			device,
			(uint)width,
			(uint)height,
			TextureFormat.R8G8B8A8Unorm,
			TextureUsageFlags.Sampler
		);
	}

	public static void SetTextureData(ResourceUploader uploader, Texture texture, Rectangle bounds, ReadOnlySpan<byte> data)
	{
		var dataSize = (uint)(bounds.Width * bounds.Height * 4);
		
		uploader.SetTextureData(
			new TextureRegion
			{
				Texture = texture,
				X = (uint)bounds.X,
				Y = (uint)bounds.Y,
				W = (uint)bounds.Width,
				H = (uint)bounds.Height,
				D = 1
			},
			data[..(int)dataSize],
			false
		);
	}
}
