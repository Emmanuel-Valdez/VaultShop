using System.Text.RegularExpressions;

namespace VaultShop.Web.Tests
{
	/// <summary>
	/// Test-only PDF text extractor: inflates every stream, collects glyph codes from every
	/// text-showing operator, then decodes them with each embedded font's ToUnicode map and
	/// concatenates the results. A text run that lives in one font decodes cleanly under that
	/// font's map (garbage under the others), so an asserted substring still shows up.
	/// </summary>
	public static class PdfTextExtractor
	{
		public static string Extract(byte[] pdf)
		{
			var content = System.Text.Encoding.Latin1.GetString(pdf);
			var inflated = new System.Text.StringBuilder();
			var stream = 0;
			while (stream < content.Length)
			{
				var start = content.IndexOf("stream", stream, StringComparison.Ordinal);
				if (start < 0) break;
				start += 6;
				if (content[start] == '\r') start++;
				if (content[start] == '\n') start++;
				var end = content.IndexOf("endstream", start, StringComparison.Ordinal);
				if (end < 0) break;
				inflated.Append(TryInflate(content[start..end]));
				stream = end + 9;
			}
			var codes = new List<string>();
			foreach (System.Text.RegularExpressions.Match match in TextShowRegex.Matches(inflated.ToString()))
			{
				foreach (System.Text.RegularExpressions.Match hex in HexCodeRegex.Matches(match.Groups[1].Value))
				{
					var value = hex.Groups[1].Value;
					for (var i = 0; i < value.Length; i += 4)
					{
						codes.Add(value.Substring(i, Math.Min(4, value.Length - i)));
					}
				}
			}
			var text = new System.Text.StringBuilder();
			foreach (var map in BuildUnicodeMaps(inflated.ToString()))
			{
				foreach (var code in codes)
				{
					if (map.TryGetValue(Convert.ToInt32(code, 16), out var ch))
					{
						text.Append(ch);
					}
				}
				text.Append('\n');
			}
			return text.ToString();
		}

		// glyph codes are 2 bytes in the Identity-H <0000> <FFFF> space; runs like <006F0075> pack several
		private static readonly Regex TextShowRegex = new(@"\[(?<array>[0-9A-Fa-f<>\s.\-]+)\]\s*TJ", RegexOptions.Compiled);
		private static readonly Regex HexCodeRegex = new(@"<([0-9A-Fa-f]+)>", RegexOptions.Compiled);

		private static List<Dictionary<int, char>> BuildUnicodeMaps(string inflated)
		{
			var maps = new List<Dictionary<int, char>>();
			foreach (System.Text.RegularExpressions.Match cmap in Regex.Matches(inflated, @"begincmap[\s\S]*?endcmap"))
			{
				var map = new Dictionary<int, char>();
				foreach (System.Text.RegularExpressions.Match block in Regex.Matches(cmap.Value, @"beginbf(char|range)[\s\S]*?endbf(char|range)"))
				{
					if (block.Value.Contains("beginbfrange"))
					{
						// beginbfrange: (<srcStart>) (<srcEnd>) (<dstStart>) — dst grows with the source span
						foreach (System.Text.RegularExpressions.Match triple in Regex.Matches(block.Value,
							@"<([0-9A-Fa-f]{2,4})>\s*<([0-9A-Fa-f]{2,4})>\s*<([0-9A-Fa-f]{2,4})>"))
						{
							var code = Convert.ToInt32(triple.Groups[1].Value, 16);
							var count = Convert.ToInt32(triple.Groups[2].Value, 16) - code;
							var start = Convert.ToInt32(triple.Groups[3].Value, 16);
							for (var offset = 0; offset <= count; offset++)
							{
								map[code + offset] = (char)(start + offset);
							}
						}
					}
					else
					{
						foreach (System.Text.RegularExpressions.Match pair in Regex.Matches(block.Value, @"<([0-9A-Fa-f]{2,4})>\s*<([0-9A-Fa-f]{2,4})>"))
						{
							map[Convert.ToInt32(pair.Groups[1].Value, 16)] = (char)Convert.ToInt32(pair.Groups[2].Value, 16);
						}
					}
				}
				maps.Add(map);
			}
			return maps;
		}

		private static string TryInflate(string raw)
		{
			var bytes = System.Text.Encoding.Latin1.GetBytes(raw);
			try
			{
				using var input = new System.IO.MemoryStream(bytes);
				using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
				using var output = new System.IO.MemoryStream();
				zlib.CopyTo(output);
				return System.Text.Encoding.Latin1.GetString(output.ToArray());
			}
			catch (System.IO.InvalidDataException)
			{
				return raw;
			}
		}
	}
}
