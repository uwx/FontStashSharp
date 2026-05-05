using System;
using System.Numerics;
using System.Runtime.InteropServices;
using FontStashSharp.Interfaces;
using MoonWorks.Graphics;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace FontStashSharp.MoonWorks;

/// <summary>
/// MoonWorks implementation of IFontStashRenderer2 that batches textured quads
/// for text rendering into the caller's render pass.
/// </summary>
public class MoonWorksRenderer : IFontStashRenderer2, IDisposable
{
	private const int MaxQuads = 2048;
	private const int MaxVertices = MaxQuads * 4;
	private const int MaxIndices = MaxQuads * 6;

	public GraphicsDevice GraphicsDevice => _device;

	private readonly GraphicsDevice _device;
	private readonly MoonWorksTexture2DManager _textureManager;

	private readonly GpuBuffer _vertexBuffer;
	private readonly GpuBuffer _indexBuffer;
	private readonly Sampler _sampler;

	private Vertex[] _vertices = new Vertex[MaxVertices];
	private int _quadCount;
	private object _currentTexture;

	// Set by the caller before rendering text
	private RenderPass _renderPass;
	private CommandBuffer _commandBuffer;
	private GraphicsPipeline _pipeline;

	public MoonWorksRenderer(GraphicsDevice device)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));
		_textureManager = new MoonWorksTexture2DManager(device);

		_vertexBuffer = GpuBuffer.Create<Vertex>(device, BufferUsageFlags.Vertex, MaxVertices);
		_indexBuffer = GpuBuffer.Create<ushort>(device, BufferUsageFlags.Index, MaxIndices);
		_sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);

		// Build static index buffer (quad indices)
		var transferBuffer = TransferBuffer.Create<ushort>(device, TransferBufferUsage.Upload, MaxIndices);
		var indices = transferBuffer.Map<ushort>(false);
		for (int i = 0; i < MaxQuads; i++)
		{
			int vi = i * 4;
			int ii = i * 6;
			indices[ii + 0] = (ushort)(vi + 0);
			indices[ii + 1] = (ushort)(vi + 1);
			indices[ii + 2] = (ushort)(vi + 2);
			indices[ii + 3] = (ushort)(vi + 0);
			indices[ii + 4] = (ushort)(vi + 2);
			indices[ii + 5] = (ushort)(vi + 3);
		}
		transferBuffer.Unmap();

		var cmd = device.AcquireCommandBuffer();
		var copyPass = cmd.BeginCopyPass();
		copyPass.UploadToBuffer(transferBuffer, _indexBuffer, false);
		cmd.EndCopyPass(copyPass);
		device.Submit(cmd);
		transferBuffer.Dispose();
	}

	/// <summary>
	/// Call before drawing text. Sets up the render context.
	/// </summary>
	public void Begin(CommandBuffer commandBuffer, RenderPass renderPass, GraphicsPipeline pipeline, Matrix4x4 transformMatrix)
	{
		_commandBuffer = commandBuffer;
		_renderPass = renderPass;
		_pipeline = pipeline;
		_quadCount = 0;
		_currentTexture = null;

		commandBuffer.PushVertexUniformData(transformMatrix);
	}

	/// <summary>
	/// Call after drawing text. Flushes any remaining quads.
	/// </summary>
	public void End()
	{
		Flush();
		_renderPass = null;
		_commandBuffer = null;
		_pipeline = null;
	}

	public void DrawQuad(Texture texture, ref Vertex topLeft, ref Vertex topRight, ref Vertex bottomLeft, ref Vertex bottomRight)
	{
		if (texture != _currentTexture)
		{
			Flush();
			_currentTexture = texture;
		}

		if (_quadCount >= MaxQuads)
			Flush();

		int vi = _quadCount * 4;
		_vertices[vi + 0] = topLeft;
		_vertices[vi + 1] = topRight;
		_vertices[vi + 2] = bottomRight;
		_vertices[vi + 3] = bottomLeft;
		_quadCount++;
	}

	private void Flush()
	{
		if (_quadCount == 0 || _renderPass == null)
			return;

		int vertexCount = _quadCount * 4;
		uint dataSize = (uint)(vertexCount * Marshal.SizeOf<Vertex>());

		var transferBuffer = TransferBuffer.Create<Vertex>(_device, TransferBufferUsage.Upload, (uint)vertexCount);
		var span = transferBuffer.Map<Vertex>(false);
		_vertices.AsSpan(0, vertexCount).CopyTo(span);
		transferBuffer.Unmap();

		var copyCmd = _device.AcquireCommandBuffer();
		var copyPass = copyCmd.BeginCopyPass();
		copyPass.UploadToBuffer(
			new TransferBufferLocation(transferBuffer, 0),
			new BufferRegion(_vertexBuffer, 0, dataSize),
			true
		);
		copyCmd.EndCopyPass(copyPass);
		_device.Submit(copyCmd);

		_renderPass.BindGraphicsPipeline(_pipeline);
		_renderPass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));
		_renderPass.BindIndexBuffer(new BufferBinding(_indexBuffer, 0), IndexElementSize.Sixteen);

		var tex = (Texture)_currentTexture;
		_renderPass.BindFragmentSamplers(new TextureSamplerBinding(tex, _sampler));

		_renderPass.DrawIndexedPrimitives(
			(uint)(_quadCount * 6),
			1,
			0,
			0,
			0
		);

		transferBuffer.Dispose();
		_quadCount = 0;
	}

	public void Dispose()
	{
		_vertexBuffer?.Dispose();
		_indexBuffer?.Dispose();
		_sampler?.Dispose();
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Vertex : IVertexType
	{
		public Vector3 Position;
		public VertexStructs.Ubyte4Norm Color;
		public Vector2 TextureCoordinate;

		// public Vertex(VertexPositionColorTexture v)
		// {
		// 	Position = v.Position;
		// 	Color = new VertexStructs.Ubyte4Norm(
		// 		v.Color.R / 255f,
		// 		v.Color.G / 255f,
		// 		v.Color.B / 255f,
		// 		v.Color.A / 255f
		// 	);
		// 	TexCoord = v.TextureCoordinate;
		// }

		public static VertexElementFormat[] Formats => [
			VertexElementFormat.Float3,    // Position
			VertexElementFormat.Ubyte4Norm, // Color
			VertexElementFormat.Float2     // TexCoord
		];

		public static uint[] Offsets => [
			0,
			(uint)Marshal.OffsetOf<Vertex>(nameof(Color)),
			(uint)Marshal.OffsetOf<Vertex>(nameof(TextureCoordinate))
		];
	}
}
