using System;
using System.Drawing;
using System.Runtime.InteropServices;
using FontStashSharp.Interfaces;
using MoonWorks.Graphics;

namespace FontStashSharp.MoonWorks;

/// <summary>
/// Manages texture creation and data upload for FontStashSharp glyph atlases using MoonWorks.
/// </summary>
public class MoonWorksTexture2DManager : ITexture2DManager
{
	private readonly GraphicsDevice _device;

	public MoonWorksTexture2DManager(GraphicsDevice device)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));
	}

	public object CreateTexture(int width, int height)
	{
		return Texture.Create2D(
			_device,
			(uint)width,
			(uint)height,
			TextureFormat.R8G8B8A8Unorm,
			TextureUsageFlags.Sampler
		);
	}

	public Point GetTextureSize(object texture)
	{
		var tex = (Texture)texture;
		return new Point((int)tex.Width, (int)tex.Height);
	}

	public void SetTextureData(object texture, Rectangle bounds, byte[] data)
	{
		var tex = (Texture)texture;
		var dataSize = (uint)(bounds.Width * bounds.Height * 4);

		var transferBuffer = TransferBuffer.Create<byte>(_device, TransferBufferUsage.Upload, dataSize);
		var span = transferBuffer.Map<byte>(false);
		data.AsSpan(0, (int)dataSize).CopyTo(span);
		transferBuffer.Unmap();

		var cmd = _device.AcquireCommandBuffer();
		var copyPass = cmd.BeginCopyPass();

		copyPass.UploadToTexture(
			new TextureTransferInfo
			{
				TransferBuffer = transferBuffer.Handle,
				Offset = 0,
			},
			new TextureRegion
			{
				Texture = tex.Handle,
				X = (uint)bounds.X,
				Y = (uint)bounds.Y,
				W = (uint)bounds.Width,
				H = (uint)bounds.Height,
				D = 1
			},
			false
		);

		cmd.EndCopyPass(copyPass);
		_device.Submit(cmd);
		transferBuffer.Dispose();
	}
}
