using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MSCLoader;
using UnityEngine;

namespace KoreanPlates
{
	public class KoreanPlates : Mod
	{
		public override string ID => "KoreanPlates";
		public override string Name => "Korean License Plates";
		public override string Version => "1.0";
		public override string Author => "windows200114-debug";

		// Plate text of the player's car.
		public const string PlayerPlate = "360무2934";

		// Must match tools/make_atlas.py
		const string Chars = "0123456789" + "가나다라마거너더러머버서어저고노도로모보소오조구누두루무부수우주하허호" + "KOR";
		const string Letters = "가나다라마거너더러머버서어저고노도로모보소오조구누두루무부수우주";
		const int CellW = 128, CellH = 192;

		const int PlateH = 256; // plate design height in px; width follows the mesh aspect

		Texture2D atlas;
		Color32[] atlasPixels;
		readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
		readonly Dictionary<int, string> assigned = new Dictionary<int, string>();
		float nextScan;
		GameObject satsuma;

		public override void ModSetup()
		{
			SetupFunction(Setup.OnLoad, Mod_OnLoad);
			SetupFunction(Setup.Update, Mod_Update);
		}

		void Mod_OnLoad()
		{
			try
			{
				var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
				using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("KoreanPlates.glyphs.png"))
				using (var ms = new MemoryStream())
				{
					var buf = new byte[8192]; int n;
					while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
					tex.LoadImage(ms.ToArray());
				}
				atlas = tex;
				atlasPixels = tex.GetPixels32();
				satsuma = GameObject.Find("SATSUMA(557kg, 248)");
			}
			catch (Exception e)
			{
				ModConsole.Error("KoreanPlates: failed to load glyph atlas: " + e);
			}
		}

		void Mod_Update()
		{
			if (atlas == null || Time.time < nextScan) return;
			nextScan = Time.time + 1f;
			ScanSatsuma();

			// Plates are parts that can be (re)spawned, and RegPlateGen rewrites the texture on start,
			// so keep checking and re-apply when the game's texture is back.
			foreach (var gen in UnityEngine.Object.FindObjectsOfType<RegPlateGen>())
			{
				var rend = gen.GetComponent<Renderer>();
				if (rend == null) continue;

				int id = gen.GetInstanceID();
				string text;
				if (!assigned.TryGetValue(id, out text))
				{
					text = IsPlayerCar(gen.transform) ? PlayerPlate : FromGamePlate(gen.PlateString);
					assigned[id] = text;
					ModConsole.Print("KoreanPlates: " + gen.transform.root.name + "/" + gen.name + " " + gen.PlateString + " -> " + text);
				}
				var tex = GetPlate(text, rend);
				var mat = rend.material;
				if (mat.mainTexture != tex)
				{
					mat.mainTexture = tex;
				// the texture is laid out for the raw mesh UVs; drop any tiling/offset the original material used
				mat.mainTextureScale = Vector2.one;
				mat.mainTextureOffset = Vector2.zero;
					// anti-reflective plate: matte surface, no specular highlight
					if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
					if (mat.HasProperty("_Shininess")) mat.SetFloat("_Shininess", 0.01f);
					if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
				}
			}
		}

		// The Satsuma's own plates are not RegPlateGen objects (texture is baked), so match them by name/texture.
		readonly HashSet<int> satsumaDone = new HashSet<int>();
		bool satsumaDumped;

		void ScanSatsuma()
		{
			if (satsuma == null) satsuma = GameObject.Find("SATSUMA(557kg, 248)");
			if (satsuma == null) return;
						foreach (var rend in satsuma.GetComponentsInChildren<Renderer>(true))
			{
				var path = PathOf(rend.transform);
				string texName = "";
				var shared = rend.sharedMaterial;
				if (shared != null && shared.mainTexture != null) texName = shared.mainTexture.name;
				string n = (rend.name + " " + texName).ToLower();
				bool looksLikePlate = n.Contains("plate") || n.Contains("vbx") || n.Contains("licen") || n.Contains("regist");
				if (!satsumaDumped && (looksLikePlate || n.Contains("reg")))
					ModConsole.Print("KoreanPlates: candidate " + path + " tex=" + texName);
				if (!looksLikePlate) continue;
				if (rend.GetComponent<RegPlateGen>() != null) continue; // handled above
				var tex = GetPlate(PlayerPlate, rend);
				Vector2 oldScale = shared != null ? shared.mainTextureScale : Vector2.one, oldOffset = shared != null ? shared.mainTextureOffset : Vector2.zero;
				var mat = rend.material;
				if (mat.mainTexture == tex) continue;
				mat.mainTexture = tex;
				// the texture is laid out for the raw mesh UVs; drop any tiling/offset the original material used
				mat.mainTextureScale = Vector2.one;
				mat.mainTextureOffset = Vector2.zero;
				if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
				if (mat.HasProperty("_Shininess")) mat.SetFloat("_Shininess", 0.01f);
				if (satsumaDone.Add(rend.GetInstanceID()))
					ModConsole.Print("KoreanPlates: Satsuma plate replaced " + path + " tex=" + texName + " tiling=" + oldScale + " offset=" + oldOffset);
			}
			satsumaDumped = true;
		}

		static string PathOf(Transform t)
		{
			string p = t.name;
			while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
			return p;
		}

		bool IsPlayerCar(Transform t)
		{
			if (satsuma == null) satsuma = GameObject.Find("SATSUMA(557kg, 248)");
			for (; t != null; t = t.parent)
			{
				if (satsuma != null && t.gameObject == satsuma) return true;
				if (t.name.StartsWith("SATSUMA")) return true;
			}
			return false;
		}

		// Stable fake Korean number derived from the original plate so each car keeps its plate.
		static string FromGamePlate(string original)
		{
			int h = 17;
			foreach (char c in original ?? "") h = h * 31 + c;
			var r = new System.Random(h);
			string number = (r.Next(0, 2) == 0 ? r.Next(10, 100) : r.Next(100, 1000)).ToString();
			return number + Letters[r.Next(Letters.Length)] + r.Next(1000, 10000);
		}

		// Plate geometry of a renderer: UV rectangle its mesh samples, and the rectangle's physical aspect ratio.
		static void PlateGeometry(Renderer rend, out float u0, out float u1, out float v0, out float v1, out float aspect)
		{
			u0 = 0; u1 = 1; v0 = 0; v1 = 1; aspect = 4.7f; // 520x110 mm
			var mf = rend.GetComponent<MeshFilter>();
			if (mf == null || mf.sharedMesh == null) return;
			var uv = mf.sharedMesh.uv;
			var vs = mf.sharedMesh.vertices;
			if (uv == null || uv.Length == 0 || uv.Length != vs.Length) return;
			u0 = v0 = float.MaxValue; u1 = v1 = float.MinValue;
			foreach (var p in uv)
			{
				u0 = Mathf.Min(u0, p.x); u1 = Mathf.Max(u1, p.x);
				v0 = Mathf.Min(v0, p.y); v1 = Mathf.Max(v1, p.y);
			}
			if (u1 - u0 < 0.001f || v1 - v0 < 0.001f) { u0 = v0 = 0; u1 = v1 = 1; return; }
			Vector3 pu0 = Vector3.zero, pu1 = Vector3.zero, pv0 = Vector3.zero, pv1 = Vector3.zero;
			int nu0 = 0, nu1 = 0, nv0 = 0, nv1 = 0;
			float eu = (u1 - u0) * 0.02f, ev = (v1 - v0) * 0.02f;
			for (int i = 0; i < uv.Length; i++)
			{
				if (uv[i].x < u0 + eu) { pu0 += vs[i]; nu0++; }
				if (uv[i].x > u1 - eu) { pu1 += vs[i]; nu1++; }
				if (uv[i].y < v0 + ev) { pv0 += vs[i]; nv0++; }
				if (uv[i].y > v1 - ev) { pv1 += vs[i]; nv1++; }
			}
			if (nu0 == 0 || nu1 == 0 || nv0 == 0 || nv1 == 0) return;
			var sc = rend.transform.lossyScale;
			float w = Vector3.Scale(pu1 / nu1 - pu0 / nu0, sc).magnitude;
			float h = Vector3.Scale(pv1 / nv1 - pv0 / nv0, sc).magnitude;
			if (w > 0.0001f && h > 0.0001f) aspect = Mathf.Clamp(w / h, 1.5f, 8f);
		}

		Texture2D GetPlate(string text, Renderer rend)
		{
			float u0, u1, v0, v1, aspect;
			PlateGeometry(rend, out u0, out u1, out v0, out v1, out aspect);
			string key = text + "|" + u0.ToString("F3") + u1.ToString("F3") + v0.ToString("F3") + v1.ToString("F3") + aspect.ToString("F2");
			Texture2D tex;
			if (cache.TryGetValue(key, out tex) && tex != null) return tex;
			tex = Render(text, u0, u1, v0, v1, aspect);
			cache[key] = tex;
			return tex;
		}

		// Korean plate (520x110 mm look): black frame, white matte face, blue KOR strip on the left,
		// "123가 4568" with a wider gap before the last four digits. Drawn into the UV rectangle the plate mesh uses.
		Texture2D Render(string text, float u0, float u1, float v0, float v1, float aspect)
		{
			int ph = PlateH, pw = Mathf.RoundToInt(PlateH * aspect);
			int tw = Mathf.Min(2048, Mathf.Max(pw, Mathf.RoundToInt(pw / (u1 - u0))));
			int th = Mathf.Min(2048, Mathf.Max(ph, Mathf.RoundToInt(ph / (v1 - v0))));
			int ox = Mathf.RoundToInt(u0 * tw), oy = Mathf.RoundToInt(v0 * th);
			pw = Mathf.RoundToInt((u1 - u0) * tw); ph = Mathf.RoundToInt((v1 - v0) * th);

			var white = new Color32(240, 240, 235, 255);
			var black = new Color32(14, 14, 14, 255);
			var blue = new Color32(22, 66, 160, 255);
			var px = new Color32[tw * th];
			for (int i = 0; i < px.Length; i++) px[i] = white;

			int B = Mathf.Max(4, ph / 22); // frame thickness
			FillRect(px, tw, th, ox, oy, 0, 0, pw, ph, black);
			FillRect(px, tw, th, ox, oy, B, B, pw - B, ph - B, white);

			// blue strip with emblem + KOR
			int stripW = (int)(ph * 0.52f);
			FillRect(px, tw, th, ox, oy, B, B, B + stripW, ph - B, blue);
			int ecx = ox + B + stripW / 2, ecy = oy + (int)(ph * 0.68f), er = (int)(stripW * 0.30f);
			for (int y = ecy - er; y <= ecy + er; y++)
				for (int x = ecx - er; x <= ecx + er; x++)
				{
					int dx = x - ecx, dy = y - ecy, d2 = dx * dx + dy * dy;
					if (x < 0 || y < 0 || x >= tw || y >= th || d2 > er * er) continue;
					px[y * tw + x] = d2 > (er * 0.78f) * (er * 0.78f) ? new Color32(235, 240, 250, 255) : new Color32(70, 120, 200, 255);
				}
			float kh = ph * 0.22f, kw = kh * 0.60f;
			float kx = ox + B + stripW / 2f - kw * 1.5f, ky = oy + ph * 0.24f;
			for (int i = 0; i < 3; i++)
				DrawCell(px, tw, th, Chars.IndexOf("KOR"[i]), kx + kw * (i + 0.5f), ky, kw, kh, new Color32(240, 245, 255, 255));

			// characters
			float faceX0 = B + stripW + ph * 0.10f, faceX1 = pw - B - ph * 0.08f;
			float h = ph * 0.64f;
			float digitW = 0.60f, hangulW = 0.84f, gapSmall = 0.0f, gapBig = 0.10f;
			float total = 0;
			for (int i = 0; i < text.Length; i++)
				total += (char.IsDigit(text[i]) ? digitW : hangulW) + (i < text.Length - 1 ? GapAfter(text, i, gapSmall, gapBig) : 0f);
			float availW = faceX1 - faceX0;
			if (total * h > availW) h = availW / total;
			float cur = faceX0 + (availW - total * h) / 2f;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				bool digit = char.IsDigit(c);
				float cw = (digit ? digitW : hangulW) * h;
				float chH = digit ? h : h * 0.82f;
				DrawCell(px, tw, th, Chars.IndexOf(c), ox + cur + cw / 2f, oy + ph * 0.5f, cw, chH, black);
				cur += cw + (i < text.Length - 1 ? GapAfter(text, i, gapSmall, gapBig) * h : 0f);
			}

			var tex = new Texture2D(tw, th, TextureFormat.ARGB32, true);
			tex.SetPixels32(px);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Trilinear;
			tex.anisoLevel = 4;
			tex.Apply(true);
			return tex;
		}

		static void FillRect(Color32[] px, int tw, int th, int ox, int oy, int x0, int y0, int x1, int y1, Color32 c)
		{
			for (int y = Mathf.Max(0, oy + y0); y < Mathf.Min(th, oy + y1); y++)
				for (int x = Mathf.Max(0, ox + x0); x < Mathf.Min(tw, ox + x1); x++)
					px[y * tw + x] = c;
		}

		// Wider gap after the hangul letter (separates the four-digit serial).
		static float GapAfter(string text, int i, float small, float big)
		{
			return !char.IsDigit(text[i]) ? big : small;
		}

		void DrawCell(Color32[] px, int tw, int th, int idx, float cx, float cy, float width, float height, Color32 col)
		{
			int hw = (int)(width / 2f), hh = (int)(height / 2f);
			int atlasW = CellW * Chars.Length;
			for (int dy = -hh; dy < hh; dy++)
			{
				int y = (int)cy + dy;
				if (y < 0 || y >= th) continue;
				float v = (dy + hh) / (2f * hh); // 0..1 bottom->top of the cell (texture origin is bottom-left)
				int ay = Mathf.Clamp((int)(v * CellH), 0, CellH - 1);
				for (int dx = -hw; dx < hw; dx++)
				{
					int x = (int)cx + dx;
					if (x < 0 || x >= tw) continue;
					float u = (dx + hw) / (2f * hw);
					int ax = idx * CellW + Mathf.Clamp((int)(u * CellW), 0, CellW - 1);
					byte a = atlasPixels[ay * atlasW + ax].a;
					if (a == 0) continue;
					int p = y * tw + x;
					var d = px[p];
					px[p] = new Color32(
						(byte)((col.r * a + d.r * (255 - a)) / 255),
						(byte)((col.g * a + d.g * (255 - a)) / 255),
						(byte)((col.b * a + d.b * (255 - a)) / 255), 255);
				}
			}
		}
	}
}
