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

	public static void SetTextureData(Texture texture, Rectangle bounds, byte[] data)
	{
		var dataSize = (uint)(bounds.Width * bounds.Height * 4);

		using var transferBuffer = TransferBuffer.Create<byte>(texture.Device, TransferBufferUsage.Upload, dataSize);
		var span = transferBuffer.Map<byte>(false);
		data.AsSpan(0, (int)dataSize).CopyTo(span);
		transferBuffer.Unmap();

		var cmd = texture.Device.AcquireCommandBuffer();
		var copyPass = cmd.BeginCopyPass();

		copyPass.UploadToTexture(
			new TextureTransferInfo
			{
				TransferBuffer = transferBuffer.Handle,
				Offset = 0,
			},
			new TextureRegion
			{
				Texture = texture.Handle,
				X = (uint)bounds.X,
				Y = (uint)bounds.Y,
				W = (uint)bounds.Width,
				H = (uint)bounds.Height,
				D = 1
			},
			false
		);

		cmd.EndCopyPass(copyPass);
		texture.Device.Submit(cmd);
	}
}
