using UnityEngine;
using UnityEngine.UI;

namespace AppUI
{
	/// <summary>Rounded-rectangle filled panel with configurable corner radius.</summary>
	public class UiRoundedRect : Graphic
	{
		const int CornerSegments = 10;

		[SerializeField] float radius = 12f;
		public float Radius
		{
			get => radius;
			set { radius = Mathf.Max(0f, value); SetVerticesDirty(); }
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var rect = GetPixelAdjustedRect();
			float r = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
			AddVertex(vh, rect.center);
			AddCorner(vh, new Vector2(rect.xMax - r, rect.yMax - r), r, 0f, 90f);
			AddCorner(vh, new Vector2(rect.xMin + r, rect.yMax - r), r, 90f, 180f);
			AddCorner(vh, new Vector2(rect.xMin + r, rect.yMin + r), r, 180f, 270f);
			AddCorner(vh, new Vector2(rect.xMax - r, rect.yMin + r), r, 270f, 360f);
			int n = vh.currentVertCount - 1;
			for (int i = 1; i <= n; i++)
			{
				int next = i == n ? 1 : i + 1;
				vh.AddTriangle(0, i, next);
			}
		}

		void AddCorner(VertexHelper vh, Vector2 center, float r, float a0, float a1)
		{
			for (int i = 0; i <= CornerSegments; i++)
			{
				float t = i / (float)CornerSegments;
				float a = Mathf.Lerp(a0, a1, t) * Mathf.Deg2Rad;
				AddVertex(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
			}
		}

		void AddVertex(VertexHelper vh, Vector2 position)
		{
			var v = UIVertex.simpleVert;
			v.position = position;
			v.color = color;
			vh.AddVert(v);
		}
	}

	/// <summary>Filled circle / ellipse (uses full rect bounds).</summary>
	public class UiCircle : Graphic
	{
		const int Segments = 64;

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var rect = GetPixelAdjustedRect();
			var c = rect.center;
			float rx = rect.width * 0.5f;
			float ry = rect.height * 0.5f;

			var center = UIVertex.simpleVert;
			center.position = c;
			center.color = color;
			vh.AddVert(center);

			for (int i = 0; i <= Segments; i++)
			{
				float a = i / (float)Segments * Mathf.PI * 2f;
				var v = UIVertex.simpleVert;
				v.position = new Vector2(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry);
				v.color = color;
				vh.AddVert(v);
			}
			for (int i = 1; i <= Segments; i++)
			{
				vh.AddTriangle(0, i, i + 1);
			}
		}
	}

	/// <summary>Linear gradient panel between two colors along a configurable direction.</summary>
	public class UiLinearGradient : Graphic
	{
		public Color colorA = Color.white;
		public Color colorB = Color.black;
		/// <summary>Direction in degrees (0 = left→right, 90 = bottom→top, 180 = right→left).</summary>
		public float angleDegrees = 90f;

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var rect = GetPixelAdjustedRect();
			float a = angleDegrees * Mathf.Deg2Rad;
			Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			Vector2 c = rect.center;
			float maxProj = Mathf.Abs(dir.x) * rect.width * 0.5f + Mathf.Abs(dir.y) * rect.height * 0.5f;

			Vector2[] corners = {
				new Vector2(rect.xMin, rect.yMin),
				new Vector2(rect.xMax, rect.yMin),
				new Vector2(rect.xMax, rect.yMax),
				new Vector2(rect.xMin, rect.yMax)
			};
			for (int i = 0; i < 4; i++)
			{
				float p = Vector2.Dot(corners[i] - c, dir);
				float t = Mathf.Clamp01((p + maxProj) / (2f * maxProj));
				var v = UIVertex.simpleVert;
				v.position = corners[i];
				v.color = Color.Lerp(colorA, colorB, t);
				vh.AddVert(v);
			}
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(2, 3, 0);
		}
	}

	/// <summary>Three-stop diagonal gradient (used for the warm peach→amber→deep auth panel).</summary>
	public class UiTriGradient : Graphic
	{
		public Color colorTopLeft = Color.white;
		public Color colorTopRight = Color.white;
		public Color colorBottomRight = Color.black;
		public Color colorBottomLeft = Color.black;

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var rect = GetPixelAdjustedRect();
			Add(vh, new Vector2(rect.xMin, rect.yMax), colorTopLeft);
			Add(vh, new Vector2(rect.xMax, rect.yMax), colorTopRight);
			Add(vh, new Vector2(rect.xMax, rect.yMin), colorBottomRight);
			Add(vh, new Vector2(rect.xMin, rect.yMin), colorBottomLeft);
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(2, 3, 0);
		}

		static void Add(VertexHelper vh, Vector2 position, Color color)
		{
			var v = UIVertex.simpleVert;
			v.position = position;
			v.color = color;
			vh.AddVert(v);
		}
	}

	/// <summary>Subtle radial vignette — bright at center, fades to transparent at edges.</summary>
	public class UiRadialGlow : Graphic
	{
		public Color centerColor = new Color(1f, 1f, 1f, 0.18f);
		public Color edgeColor = new Color(1f, 1f, 1f, 0f);
		public float radiusScale = 0.6f;
		const int Rings = 6;
		const int Segments = 48;

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var rect = GetPixelAdjustedRect();
			Vector2 c = rect.center;
			float r = Mathf.Min(rect.width, rect.height) * 0.5f * radiusScale;

			var center = UIVertex.simpleVert;
			center.position = c;
			center.color = centerColor;
			vh.AddVert(center);

			for (int ring = 1; ring <= Rings; ring++)
			{
				float t = ring / (float)Rings;
				float ringRadius = Mathf.Lerp(0f, r, t);
				Color ringColor = Color.Lerp(centerColor, edgeColor, t);
				for (int i = 0; i < Segments; i++)
				{
					float a = i / (float)Segments * Mathf.PI * 2f;
					var v = UIVertex.simpleVert;
					v.position = new Vector2(c.x + Mathf.Cos(a) * ringRadius, c.y + Mathf.Sin(a) * ringRadius);
					v.color = ringColor;
					vh.AddVert(v);
				}
			}

			// Triangulate rings (fan to center for innermost ring, strips for outer)
			for (int i = 0; i < Segments; i++)
			{
				int next = (i + 1) % Segments;
				vh.AddTriangle(0, 1 + i, 1 + next);
			}
			for (int ring = 1; ring < Rings; ring++)
			{
				int innerStart = 1 + (ring - 1) * Segments;
				int outerStart = 1 + ring * Segments;
				for (int i = 0; i < Segments; i++)
				{
					int next = (i + 1) % Segments;
					vh.AddTriangle(innerStart + i, outerStart + i, outerStart + next);
					vh.AddTriangle(innerStart + i, outerStart + next, innerStart + next);
				}
			}
		}
	}

	/// <summary>Rounded-rectangle outline (stroke only).</summary>
	public class UiRoundedOutline : Graphic
	{
		const int CornerSegments = 10;
		[SerializeField] float radius = 12f;
		[SerializeField] float thickness = 1f;

		public float Radius { get => radius; set { radius = Mathf.Max(0f, value); SetVerticesDirty(); } }
		public float Thickness { get => thickness; set { thickness = Mathf.Max(0f, value); SetVerticesDirty(); } }

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var rect = GetPixelAdjustedRect();
			float r = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
			float t = Mathf.Min(thickness, r);

			int totalOuter = 0;
			AddRingPair(vh, rect, r, t, ref totalOuter);

			int n = totalOuter;
			for (int i = 0; i < n; i++)
			{
				int next = (i + 1) % n;
				int outerA = i;
				int outerB = next;
				int innerA = i + n;
				int innerB = next + n;
				vh.AddTriangle(outerA, innerA, outerB);
				vh.AddTriangle(outerB, innerA, innerB);
			}
		}

		void AddRingPair(VertexHelper vh, Rect rect, float r, float t, ref int outerCount)
		{
			Vector2[] outerCenters = {
				new Vector2(rect.xMax - r, rect.yMax - r),
				new Vector2(rect.xMin + r, rect.yMax - r),
				new Vector2(rect.xMin + r, rect.yMin + r),
				new Vector2(rect.xMax - r, rect.yMin + r)
			};
			float[] starts = { 0f, 90f, 180f, 270f };

			// outer ring
			for (int c = 0; c < 4; c++)
			{
				for (int i = 0; i <= CornerSegments; i++)
				{
					if (c > 0 && i == 0) continue; // dedup
					float a = Mathf.Lerp(starts[c], starts[c] + 90f, i / (float)CornerSegments) * Mathf.Deg2Rad;
					AddVertex(vh, outerCenters[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
				}
			}
			outerCount = vh.currentVertCount;
			// inner ring
			float ri = Mathf.Max(0f, r - t);
			for (int c = 0; c < 4; c++)
			{
				for (int i = 0; i <= CornerSegments; i++)
				{
					if (c > 0 && i == 0) continue;
					float a = Mathf.Lerp(starts[c], starts[c] + 90f, i / (float)CornerSegments) * Mathf.Deg2Rad;
					AddVertex(vh, outerCenters[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ri);
				}
			}
		}

		void AddVertex(VertexHelper vh, Vector2 position)
		{
			var v = UIVertex.simpleVert;
			v.position = position;
			v.color = color;
			vh.AddVert(v);
		}
	}
}
