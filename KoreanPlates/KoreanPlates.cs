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
		const string Chars = "0123456789" + "가나다라마거너더러머버서어저고노도로모보소오조구누두루무부수우주하허호";
		const string Letters = "가나다라마거너더러머버서어저고노도로모보소오조구누두루무부수우주";
		const int CellW = 128, CellH = 192;

		// Plate canvas, 520x110 mm ratio
		const int PW = 1040, PH = 220;

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
				var tex = GetPlate(text);
				var mat = rend.material;
				if (mat.mainTexture != tex)
				{
					mat.mainTexture = tex;
					// anti-reflective plate: matte surface, no specular highlight
					if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
					if (mat.HasProperty("_Shininess")) mat.SetFloat("_Shininess", 0.01f);
					if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
				}
			}
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

		Texture2D GetPlate(string text)
		{
			Texture2D tex;
			if (cache.TryGetValue(text, out tex) && tex != null) return tex;
			tex = Render(text);
			cache[text] = tex;
			return tex;
		}

		// White matte plate, black characters, thin border (anti-reflective 반사방지 plate look).
		Texture2D Render(string text)
		{
			var px = new Color32[PW * PH];
			var white = new Color32(238, 238, 232, 255);
			var black = new Color32(18, 18, 18, 255);
			for (int i = 0; i < px.Length; i++) px[i] = white;

			const int B = 8;
			for (int y = 0; y < PH; y++)
				for (int x = 0; x < PW; x++)
					if (x < B || y < B || x >= PW - B || y >= PH - B)
						px[y * PW + x] = black;

			// glyph slots: digits narrower than the hangul letter
			const float digitSlot = 0.78f, hangulSlot = 1.0f;
			const int gap = 6, margin = 50;
			float height = PH - 2 * B - 36;
			float cellH = height;
			float total = 0;
			foreach (char c in text) total += char.IsDigit(c) ? digitSlot : hangulSlot;
			float availW = PW - 2 * B - 2 * margin - (text.Length - 1) * gap;
			float unit = Mathf.Min(CellW * cellH / CellH * 1.0f, availW / total); // pixel width of a 1.0 slot
			float w = total * unit + (text.Length - 1) * gap;
			float x0 = (PW - w) / 2f;
			foreach (char c in text)
			{
				float cw = (char.IsDigit(c) ? digitSlot : hangulSlot) * unit;
				int idx = Chars.IndexOf(c);
				if (idx >= 0) DrawCell(px, idx, x0 + cw / 2f, PH / 2f, cw, cellH, black);
				x0 += cw + gap;
			}

			var tex = new Texture2D(PW, PH, TextureFormat.ARGB32, true);
			tex.SetPixels32(px);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Trilinear;
			tex.anisoLevel = 4;
			tex.Apply(true);
			return tex;
		}

		void DrawCell(Color32[] px, int idx, float cx, float cy, float width, float height, Color32 col)
		{
			int hw = (int)(width / 2f), hh = (int)(height / 2f);
			int atlasW = CellW * Chars.Length;
			for (int dy = -hh; dy < hh; dy++)
			{
				int y = (int)cy + dy;
				if (y < 0 || y >= PH) continue;
				float v = (dy + hh) / (2f * hh); // 0..1 bottom->top of the cell (texture origin is bottom-left)
				int ay = Mathf.Clamp((int)(v * CellH), 0, CellH - 1);
				for (int dx = -hw; dx < hw; dx++)
				{
					int x = (int)cx + dx;
					if (x < 0 || x >= PW) continue;
					float u = (dx + hw) / (2f * hw);
					int ax = idx * CellW + Mathf.Clamp((int)(u * CellW), 0, CellW - 1);
					byte a = atlasPixels[ay * atlasW + ax].a;
					if (a == 0) continue;
					int p = y * PW + x;
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
