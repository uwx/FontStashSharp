#if MONOGAME || FNA || XNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#elif MOONWORKS
using System.Drawing;
using System.Numerics;
using Color = MoonWorks.Graphics.Color;

#else
using System.Drawing;
using System.Numerics;
using Color = FontStashSharp.FSColor;
#endif

namespace FontStashSharp.RichText
{
	public interface IRenderable
	{
		Point Size { get; }

		void Draw(FSRenderContext context, Vector2 position, Color color);
	}
}
