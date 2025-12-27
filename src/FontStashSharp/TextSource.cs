using System;
using System.Buffers;
using System.Text;

namespace FontStashSharp
{
	internal ref struct TextSource
	{
		public StringSegment StringText;
		public StringBuilder StringBuilderText;
		public ReadOnlySpan<char> SpanText;
		private int Position;

		public TextSource(ReadOnlySpan<char> text)
		{
			SpanText = text;
			StringText = default;
			StringBuilderText = null;
			Position = 0;
		}

		public TextSource(string text)
		{
			StringText = new StringSegment(text);
			StringBuilderText = null;
			Position = 0;
		}

		public TextSource(StringSegment text)
		{
			StringText = text;
			StringBuilderText = null;
			Position = 0;
		}

		public TextSource(StringBuilder text)
		{
			StringText = new StringSegment();
			StringBuilderText = text;
			Position = 0;
		}

		public bool IsNull => StringText.IsNullOrEmpty && StringBuilderText == null && SpanText.IsEmpty;

		public bool GetNextCodepoint(out int result)
		{
			result = 0;
			
			if (!SpanText.IsEmpty)
			{
				if (Position >= SpanText.Length)
				{
					return false;
				}

				var opResult = Rune.DecodeFromUtf16(SpanText[Position..], out var rune, out var charsConsumed);
				if (opResult != OperationStatus.Done)
				{
					ThrowInvalidString();
				}
				result = rune.Value;
				Position += charsConsumed;
				return true;
			}

			if (!StringText.IsNullOrEmpty)
			{
				if (Position >= StringText.Length)
				{
					return false;
				}

				var opResult = Rune.DecodeFromUtf16(StringText.String.AsSpan((StringText.Offset + Position)..), out var rune, out var charsConsumed);
				if (opResult != OperationStatus.Done)
				{
					ThrowInvalidString();
				}
				result = rune.Value;
				Position += charsConsumed;
				return true;
			}

			if (StringBuilderText != null)
			{
				if (Position >= StringBuilderText.Length)
				{
					return false;
				}

				result = StringBuilderConvertToUtf32(StringBuilderText, Position);
				Position += StringBuilderIsSurrogatePair(StringBuilderText, Position) ? 2 : 1;
				return true;
			}

			return false;

			void ThrowInvalidString()
			{
				throw new InvalidOperationException("Invalid UTF-16 sequence in TextSource.");
			}
		}

		public void Reset()
		{
			Position = 0;
		}

		private static bool StringBuilderIsSurrogatePair(StringBuilder sb, int index)
		{
			if (index + 1 < sb.Length)
				return char.IsSurrogatePair(sb[index], sb[index + 1]);
			return false;
		}

		private static int StringBuilderConvertToUtf32(StringBuilder sb, int index)
		{
			if (!char.IsHighSurrogate(sb[index]))
				return sb[index];

			return char.ConvertToUtf32(sb[index], sb[index + 1]);
		}

		public static int CalculateLength(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0;
			}

			var pos = 0;
			var result = 0;
			while(pos < text.Length)
			{
				pos += char.IsSurrogatePair(text, pos) ? 2 : 1;
				++result;
			}

			return result;
		}
	}
}
