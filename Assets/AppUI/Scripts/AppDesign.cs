using UnityEngine;

namespace AppUI
{
	public static class AppDesign
	{
		// Core surface palette (dark navy)
		public static readonly Color Background = Hex(0x0e1424);
		public static readonly Color BackgroundDeep = Hex(0x0a1020);
		public static readonly Color Surface = Hex(0x181f33);
		public static readonly Color SurfaceAlt = Hex(0x232b42);
		public static readonly Color SurfaceDeep = Hex(0x2c3653);
		public static readonly Color SurfaceLine = Hex(0x1f2740);

		// Ink (text) levels
		public static readonly Color Ink = Hex(0xeef1fa);
		public static readonly Color InkSoft = Hex(0xa0aac7);
		public static readonly Color InkFaint = Hex(0x5e688a);
		public static readonly Color InkMuted = Hex(0x3d4665);

		// Accents
		public static readonly Color Accent = Hex(0x5fc8d2);     // teal
		public static readonly Color AccentDeep = Hex(0x3aa6b1);
		public static readonly Color AccentInk = Hex(0xb3ecf2);
		public static readonly Color AccentSoft = new Color(0.373f, 0.784f, 0.824f, 0.18f);
		public static readonly Color Accent2 = Hex(0xf0b562);    // amber

		// Warm gradient (auth left panel: light peach -> mid amber -> deep amber)
		public static readonly Color WarmLight = Hex(0xf6c98a);
		public static readonly Color WarmMid = Hex(0xe89b62);
		public static readonly Color WarmDeep = Hex(0xb96a3a);
		public static readonly Color WarmInk = Hex(0xfff4e5);

		// Status
		public static readonly Color Good = Hex(0x6fd8a8);
		public static readonly Color Warn = Hex(0xf0b562);
		public static readonly Color Bad = Hex(0xff8676);

		// Decorative floating-icon colors (splash)
		public static readonly Color IconBlush = Hex(0xe07a5f);
		public static readonly Color IconBlueMic = Hex(0x6f8fcf);
		public static readonly Color IconCloud = Hex(0xa07cc8);
		public static readonly Color IconHeart = Hex(0x6fa07a);
		public static readonly Color IconStar = Hex(0x6fa07a);
		public static readonly Color IconGlobe = Hex(0xa07cc8);
		public static readonly Color IconLetterA = Hex(0x4a6ba8);
		public static readonly Color IconSmiley = Hex(0xe89b62);
		public static readonly Color IconEye = Hex(0xc8694a);

		static Font bodyFont;
		static Font logoFont;
		static Font monoFont;

		public static Font BodyFont => bodyFont ??= Font.CreateDynamicFontFromOSFont(
			new[] { "Plus Jakarta Sans", "Inter", "Segoe UI Variable", "Segoe UI", "Arial" },
			16);

		public static Font LogoFont => logoFont ??= Font.CreateDynamicFontFromOSFont(
			new[] { "Instrument Serif", "Cormorant Garamond", "Georgia", "Times New Roman" },
			16);

		public static Font MonoFont => monoFont ??= Font.CreateDynamicFontFromOSFont(
			new[] { "JetBrains Mono", "Cascadia Mono", "Consolas", "Courier New" },
			16);

		static Color Hex(int value)
		{
			float r = ((value >> 16) & 0xff) / 255f;
			float g = ((value >> 8) & 0xff) / 255f;
			float b = (value & 0xff) / 255f;
			return new Color(r, g, b, 1f);
		}
	}
}
