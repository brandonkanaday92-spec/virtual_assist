using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using AvatarSDK.MetaPerson.OculusLipSync;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppUI
{
	public class SplashSceneController : MonoBehaviour
	{
		[SerializeField] string nextSceneName = "LoginScene";
		[SerializeField] float displaySeconds = 2.5f;
		[SerializeField] bool preloadVirtualAssistScene = true;
		[SerializeField] string virtualAssistSceneName = "VirtualAssistScene";
		[SerializeField] float preloadTimeoutSeconds = 20f;

		CanvasGroup canvasGroup;
		Text statusText;
		Button tapToBeginButton;
		RectTransform progressFillRect;
		bool advanceRequested;

		void Awake()
		{
			BuildUi();
		}

		void Start()
		{
			StartCoroutine(PlaySplashThenLoad());
		}

		IEnumerator PlaySplashThenLoad()
		{
			Coroutine preloadCoroutine = preloadVirtualAssistScene ? StartCoroutine(PreloadVirtualAssistScene()) : null;

			float elapsed = 0f;
			while (elapsed < displaySeconds && !advanceRequested)
			{
				elapsed += Time.deltaTime;
				float progress = Mathf.Clamp01(elapsed / displaySeconds);
				if (progressFillRect != null)
				{
					progressFillRect.anchorMax = new Vector2(Mathf.SmoothStep(0f, 1f, progress), 1f);
				}
				statusText.text = AvatarPreloadService.IsPreloading
					? "Preloading assistant..."
					: "Preparing assistant...";
				yield return null;
			}

			if (preloadCoroutine != null)
			{
				while (AvatarPreloadService.IsPreloading)
				{
					statusText.text = "Preloading assistant...";
					yield return null;
				}
			}

			statusText.text = "Opening login...";

			float fadeElapsed = 0f;
			const float fadeDuration = 0.35f;
			while (fadeElapsed < fadeDuration)
			{
				fadeElapsed += Time.deltaTime;
				canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeDuration);
				yield return null;
			}

			SceneManager.LoadScene(nextSceneName);
		}

		IEnumerator PreloadVirtualAssistScene()
		{
			if (AvatarPreloadService.IsPreloaded || AvatarPreloadService.IsPreloading)
			{
				yield break;
			}

			AvatarPreloadService.Begin();

			var loadOperation = SceneManager.LoadSceneAsync(virtualAssistSceneName, LoadSceneMode.Additive);
			while (loadOperation != null && !loadOperation.isDone)
			{
				yield return null;
			}

			var virtualScene = SceneManager.GetSceneByName(virtualAssistSceneName);
			if (!virtualScene.IsValid())
			{
				AvatarPreloadService.Fail($"Scene was not loaded: {virtualAssistSceneName}");
				yield break;
			}

			OculusSampleSceneHandler sceneHandler = null;
			float startedAt = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - startedAt < preloadTimeoutSeconds)
			{
				foreach (var root in virtualScene.GetRootGameObjects())
				{
					sceneHandler ??= root.GetComponentInChildren<OculusSampleSceneHandler>(true);
				}

				if (sceneHandler == null)
				{
					yield return null;
					continue;
				}

				if (sceneHandler.IsAvatarReady)
				{
					break;
				}

				if (sceneHandler.AvatarLoadFailed)
				{
					AvatarPreloadService.Fail("Offline avatar failed to load.");
					yield break;
				}

				yield return null;
			}

			if (sceneHandler == null || !sceneHandler.IsAvatarReady)
			{
				AvatarPreloadService.Fail("Avatar preload timed out.");
				yield break;
			}

			var roots = new List<GameObject>(virtualScene.GetRootGameObjects());
			foreach (var root in roots)
			{
				foreach (var sceneCamera in root.GetComponentsInChildren<Camera>(true))
				{
					sceneCamera.enabled = false;
					var audioListener = sceneCamera.GetComponent<AudioListener>();
					if (audioListener != null)
					{
						audioListener.enabled = false;
					}
				}

				DontDestroyOnLoad(root);
			}

			AvatarPreloadService.Complete(roots);
		}

		// ----- UI ---------------------------------------------------------

		void BuildUi()
		{
			var canvasObject = new GameObject("Splash Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
			var canvas = canvasObject.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasGroup = canvasObject.GetComponent<CanvasGroup>();

			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			// Background
			var background = AddSolid("Background", canvas.transform, AppDesign.Background);
			FillParent(background.GetComponent<RectTransform>());

			// Subtle radial glow
			var glow = AddGraphic<UiRadialGlow>("Center Glow", background.transform);
			glow.centerColor = new Color(95f / 255f, 200f / 255f, 210f / 255f, 0.10f);
			glow.edgeColor = new Color(95f / 255f, 200f / 255f, 210f / 255f, 0f);
			glow.radiusScale = 0.8f;
			FillParent(glow.rectTransform);

			// Floating decorative icons
			BuildFloatingIcons(background.transform);

			// Top date row
			BuildDateRow(background.transform);

			// Center brand stack
			BuildCenterStack(background.transform);

			// Footer status + version row
			BuildFooter(background.transform);
		}

		// ---- Floating icons (positioned by anchored offsets from center) -----

		void BuildFloatingIcons(Transform parent)
		{
			// Orange smiley chat bubble (top-left of brand)
			AddChatBubbleIcon(parent, new Vector2(-560f, 200f), AppDesign.IconBlush);
			// Blue mic (top-right)
			AddMicIcon(parent, new Vector2(540f, 220f), AppDesign.IconBlueMic);
			// Purple cloud (top center)
			AddCloudIcon(parent, new Vector2(60f, 260f), AppDesign.IconCloud);
			// Green heart (left)
			AddHeartIcon(parent, new Vector2(-440f, 30f), AppDesign.IconHeart);
			// Green star (right)
			AddStarIcon(parent, new Vector2(440f, 100f), AppDesign.IconStar);
			// Purple globe (right-lower)
			AddGlobeIcon(parent, new Vector2(540f, -80f), AppDesign.IconGlobe);
			// Orange eye (left-lower)
			AddEyeIcon(parent, new Vector2(-460f, -120f), AppDesign.IconEye);
			// Blue letter A (bottom-left)
			AddLetterIcon(parent, new Vector2(-400f, -240f), "A", AppDesign.IconLetterA);
			// Orange smiley face (bottom-right)
			AddSmileyIcon(parent, new Vector2(500f, -250f), AppDesign.IconSmiley);
		}

		GameObject AnchorAtCenter(Transform parent, string name, Vector2 offset, Vector2 size)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			var rt = go.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = offset;
			rt.sizeDelta = size;
			return go;
		}

		void AddChatBubbleIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · ChatBubble", offset, new Vector2(72f, 72f));
			var body = AddRounded("body", root.transform, color, 18f);
			FillParent(body.GetComponent<RectTransform>());
			AddEye(body.transform, new Vector2(-12f, 6f));
			AddEye(body.transform, new Vector2(12f, 6f));
			// tiny mouth dot
			var mouth = AddCircle("mouth", body.transform, new Color(1f, 1f, 1f, 0.95f));
			var mr = mouth.GetComponent<RectTransform>();
			mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.5f);
			mr.anchoredPosition = new Vector2(0f, -8f);
			mr.sizeDelta = new Vector2(16f, 8f);
		}

		void AddEye(Transform parent, Vector2 offset)
		{
			var eye = AddCircle("eye", parent, new Color(1f, 1f, 1f, 0.95f));
			var rt = eye.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = offset;
			rt.sizeDelta = new Vector2(6f, 6f);
		}

		void AddMicIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Mic", offset, new Vector2(64f, 80f));
			// mic capsule
			var cap = AddRounded("cap", root.transform, color, 14f);
			var capRect = cap.GetComponent<RectTransform>();
			capRect.anchorMin = new Vector2(0.5f, 1f);
			capRect.anchorMax = new Vector2(0.5f, 1f);
			capRect.pivot = new Vector2(0.5f, 1f);
			capRect.anchoredPosition = new Vector2(0f, -8f);
			capRect.sizeDelta = new Vector2(28f, 44f);
			// stand
			var stand = AddSolid("stand", root.transform, color);
			var sr = stand.GetComponent<RectTransform>();
			sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
			sr.pivot = new Vector2(0.5f, 0f);
			sr.anchoredPosition = new Vector2(0f, 6f);
			sr.sizeDelta = new Vector2(4f, 14f);
			// base
			var bse = AddRounded("base", root.transform, color, 3f);
			var br = bse.GetComponent<RectTransform>();
			br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
			br.pivot = new Vector2(0.5f, 0f);
			br.anchoredPosition = new Vector2(0f, 0f);
			br.sizeDelta = new Vector2(24f, 6f);
		}

		void AddCloudIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Cloud", offset, new Vector2(72f, 48f));
			// three circles + a base
			AddCloudLobe(root.transform, new Vector2(-20f, 0f), new Vector2(34f, 34f), color);
			AddCloudLobe(root.transform, new Vector2(0f, 6f), new Vector2(40f, 40f), color);
			AddCloudLobe(root.transform, new Vector2(20f, 0f), new Vector2(32f, 32f), color);
			var bottom = AddRounded("base", root.transform, color, 10f);
			var br = bottom.GetComponent<RectTransform>();
			br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
			br.pivot = new Vector2(0.5f, 0f);
			br.anchoredPosition = new Vector2(0f, 0f);
			br.sizeDelta = new Vector2(58f, 18f);
		}

		void AddCloudLobe(Transform parent, Vector2 offset, Vector2 size, Color color)
		{
			var go = AddCircle("lobe", parent, color);
			var rt = go.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = offset;
			rt.sizeDelta = size;
		}

		void AddHeartIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Heart", offset, new Vector2(54f, 50f));
			AddCloudLobe(root.transform, new Vector2(-12f, 10f), new Vector2(28f, 28f), color);
			AddCloudLobe(root.transform, new Vector2(12f, 10f), new Vector2(28f, 28f), color);
			// tip (diamond)
			var tip = AddSolid("tip", root.transform, color);
			var tr = tip.GetComponent<RectTransform>();
			tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
			tr.anchoredPosition = new Vector2(0f, -10f);
			tr.sizeDelta = new Vector2(28f, 28f);
			tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
		}

		void AddStarIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Star", offset, new Vector2(56f, 56f));
			// rendered as a 4-point cross from two rotated squares
			var a = AddSolid("a", root.transform, color);
			var ar = a.GetComponent<RectTransform>();
			ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
			ar.sizeDelta = new Vector2(50f, 16f);
			a.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
			var b = AddSolid("b", root.transform, color);
			var brt = b.GetComponent<RectTransform>();
			brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
			brt.sizeDelta = new Vector2(50f, 16f);
			b.transform.localRotation = Quaternion.Euler(0f, 0f, 150f);
		}

		void AddGlobeIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Globe", offset, new Vector2(60f, 60f));
			var ring = AddGraphic<UiRoundedOutline>("ring", root.transform);
			ring.color = color;
			ring.Radius = 30f;
			ring.Thickness = 4f;
			FillParent(ring.rectTransform);
			// horizontal & vertical "longitude/latitude" lines
			var hr = AddSolid("h", root.transform, color);
			var hrt = hr.GetComponent<RectTransform>();
			hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
			hrt.sizeDelta = new Vector2(54f, 3f);
			var vr = AddSolid("v", root.transform, color);
			var vrt = vr.GetComponent<RectTransform>();
			vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.5f);
			vrt.sizeDelta = new Vector2(3f, 54f);
		}

		void AddEyeIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Eye", offset, new Vector2(58f, 36f));
			var outline = AddGraphic<UiRoundedOutline>("outline", root.transform);
			outline.color = color;
			outline.Radius = 18f;
			outline.Thickness = 4f;
			FillParent(outline.rectTransform);
			var pupil = AddCircle("pupil", root.transform, color);
			var pr = pupil.GetComponent<RectTransform>();
			pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
			pr.sizeDelta = new Vector2(12f, 12f);
		}

		void AddLetterIcon(Transform parent, Vector2 offset, string letter, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Letter", offset, new Vector2(56f, 56f));
			var card = AddRounded("card", root.transform, color, 14f);
			FillParent(card.GetComponent<RectTransform>());
			var letterText = AddText("letter", card.transform, letter, 32, TextAnchor.MiddleCenter, AppDesign.Ink);
			letterText.font = AppDesign.LogoFont;
			letterText.fontStyle = FontStyle.Italic;
			FillParent(letterText.rectTransform);
		}

		void AddSmileyIcon(Transform parent, Vector2 offset, Color color)
		{
			var root = AnchorAtCenter(parent, "Icon · Smiley", offset, new Vector2(56f, 56f));
			var face = AddCircle("face", root.transform, color);
			FillParent(face.GetComponent<RectTransform>());
			AddEye(face.transform, new Vector2(-9f, 6f));
			AddEye(face.transform, new Vector2(9f, 6f));
			// mouth
			var mouth = AddRounded("mouth", face.transform, new Color(1f, 1f, 1f, 0.95f), 5f);
			var mr = mouth.GetComponent<RectTransform>();
			mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.5f);
			mr.anchoredPosition = new Vector2(0f, -8f);
			mr.sizeDelta = new Vector2(20f, 6f);
		}

		// ---- Top date row -----------------------------------------------------

		void BuildDateRow(Transform parent)
		{
			var dateLabel = AddText("Date", parent, FormatDate(), 22, TextAnchor.MiddleCenter, AppDesign.InkFaint);
			dateLabel.font = AppDesign.MonoFont;
			dateLabel.fontStyle = FontStyle.Normal;
			var rt = dateLabel.rectTransform;
			rt.anchorMin = new Vector2(0.5f, 1f);
			rt.anchorMax = new Vector2(0.5f, 1f);
			rt.pivot = new Vector2(0.5f, 1f);
			rt.anchoredPosition = new Vector2(0f, -140f);
			rt.sizeDelta = new Vector2(520f, 28f);
		}

		static string FormatDate()
		{
			var date = System.DateTime.Now;
			string day = date.ToString("dddd", CultureInfo.InvariantCulture).ToUpperInvariant();
			string mon = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
			return $"{day}  ·  {date.Day:00}  {mon}  {date.Year}";
		}

		// ---- Center brand stack ----------------------------------------------

		void BuildCenterStack(Transform parent)
		{
			// Rounded teal F square
			var fSquare = AddRounded("F Square", parent, AppDesign.Accent, 22f);
			var fSquareRect = fSquare.GetComponent<RectTransform>();
			fSquareRect.anchorMin = fSquareRect.anchorMax = fSquareRect.pivot = new Vector2(0.5f, 0.5f);
			fSquareRect.anchoredPosition = new Vector2(0f, 142f);
			fSquareRect.sizeDelta = new Vector2(106f, 106f);

			var fLetter = AddText("F Letter", fSquare.transform, "F", 72, TextAnchor.MiddleCenter, AppDesign.Ink);
			fLetter.font = AppDesign.LogoFont;
			fLetter.fontStyle = FontStyle.Italic;
			FillParent(fLetter.rectTransform);

			// Tiny notification dot
			var dot = AddCircle("notif dot", fSquare.transform, AppDesign.Ink);
			var dotRect = dot.GetComponent<RectTransform>();
			dotRect.anchorMin = dotRect.anchorMax = new Vector2(1f, 1f);
			dotRect.pivot = new Vector2(0.5f, 0.5f);
			dotRect.anchoredPosition = new Vector2(-6f, -6f);
			dotRect.sizeDelta = new Vector2(12f, 12f);

			// Wordmark "Free" + italic teal "Talk" — two text components side by side
			var wordmark = new GameObject("Wordmark", typeof(RectTransform));
			wordmark.transform.SetParent(parent, false);
			var wmRect = wordmark.GetComponent<RectTransform>();
			wmRect.anchorMin = wmRect.anchorMax = wmRect.pivot = new Vector2(0.5f, 0.5f);
			wmRect.anchoredPosition = new Vector2(0f, 26f);
			wmRect.sizeDelta = new Vector2(900f, 160f);

			var free = AddText("Free", wordmark.transform, "Free", 132, TextAnchor.MiddleRight, AppDesign.Ink);
			free.font = AppDesign.LogoFont;
			free.fontStyle = FontStyle.Normal;
			var freeRect = free.rectTransform;
			freeRect.anchorMin = new Vector2(0f, 0f);
			freeRect.anchorMax = new Vector2(0.5f, 1f);
			freeRect.pivot = new Vector2(1f, 0.5f);
			freeRect.anchoredPosition = new Vector2(8f, 0f);
			freeRect.sizeDelta = new Vector2(0f, 0f);

			var talk = AddText("Talk", wordmark.transform, "Talk", 132, TextAnchor.MiddleLeft, AppDesign.Accent);
			talk.font = AppDesign.LogoFont;
			talk.fontStyle = FontStyle.Italic;
			var talkRect = talk.rectTransform;
			talkRect.anchorMin = new Vector2(0.5f, 0f);
			talkRect.anchorMax = new Vector2(1f, 1f);
			talkRect.pivot = new Vector2(0f, 0.5f);
			talkRect.anchoredPosition = new Vector2(-4f, 0f);
			talkRect.sizeDelta = new Vector2(0f, 0f);

			// Tagline
			var tagline = AddText("Tagline", parent, "Open your mouth. Find your voice.", 36, TextAnchor.MiddleCenter, AppDesign.InkSoft);
			tagline.font = AppDesign.LogoFont;
			tagline.fontStyle = FontStyle.Italic;
			var tagRect = tagline.rectTransform;
			tagRect.anchorMin = tagRect.anchorMax = tagRect.pivot = new Vector2(0.5f, 0.5f);
			tagRect.anchoredPosition = new Vector2(0f, -50f);
			tagRect.sizeDelta = new Vector2(900f, 50f);

			// Tap to begin pill button
			BuildTapToBeginButton(parent, new Vector2(0f, -150f));
		}

		void BuildTapToBeginButton(Transform parent, Vector2 anchoredPosition)
		{
			var pill = AddRounded("Tap To Begin", parent, AppDesign.Ink, 32f);
			var rt = pill.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = anchoredPosition;
			rt.sizeDelta = new Vector2(248f, 64f);

			tapToBeginButton = pill.AddComponent<Button>();
			tapToBeginButton.targetGraphic = pill.GetComponent<Graphic>();
			tapToBeginButton.onClick.AddListener(() => advanceRequested = true);

			var label = AddText("Label", pill.transform, "Tap to begin", 22, TextAnchor.MiddleLeft, AppDesign.Background);
			label.font = AppDesign.BodyFont;
			var lr = label.rectTransform;
			lr.anchorMin = new Vector2(0f, 0f);
			lr.anchorMax = new Vector2(1f, 1f);
			lr.offsetMin = new Vector2(32f, 0f);
			lr.offsetMax = new Vector2(-64f, 0f);

			// Trailing teal circle with arrow/mic
			var endDot = AddCircle("End Dot", pill.transform, AppDesign.Accent);
			var edr = endDot.GetComponent<RectTransform>();
			edr.anchorMin = edr.anchorMax = new Vector2(1f, 0.5f);
			edr.pivot = new Vector2(1f, 0.5f);
			edr.anchoredPosition = new Vector2(-10f, 0f);
			edr.sizeDelta = new Vector2(44f, 44f);

			var arrow = AddText("Arrow", endDot.transform, "→", 22, TextAnchor.MiddleCenter, AppDesign.Background);
			arrow.font = AppDesign.BodyFont;
			arrow.fontStyle = FontStyle.Bold;
			FillParent(arrow.rectTransform);
		}

		// ---- Footer (progress + version) -------------------------------------

		void BuildFooter(Transform parent)
		{
			// Status text (loading state)
			statusText = AddText("Status", parent, "Preparing assistant...", 18, TextAnchor.MiddleCenter, AppDesign.InkFaint);
			var statusRect = statusText.rectTransform;
			statusRect.anchorMin = new Vector2(0.5f, 0f);
			statusRect.anchorMax = new Vector2(0.5f, 0f);
			statusRect.pivot = new Vector2(0.5f, 0f);
			statusRect.anchoredPosition = new Vector2(0f, 96f);
			statusRect.sizeDelta = new Vector2(480f, 28f);

			// Thin progress track
			var track = AddRounded("Progress Track", parent, AppDesign.SurfaceLine, 2f);
			var trackRect = track.GetComponent<RectTransform>();
			trackRect.anchorMin = new Vector2(0.5f, 0f);
			trackRect.anchorMax = new Vector2(0.5f, 0f);
			trackRect.pivot = new Vector2(0.5f, 0f);
			trackRect.anchoredPosition = new Vector2(0f, 80f);
			trackRect.sizeDelta = new Vector2(320f, 4f);

			var fill = AddRounded("Progress Fill", track.transform, AppDesign.Accent, 2f);
			progressFillRect = fill.GetComponent<RectTransform>();
			progressFillRect.anchorMin = Vector2.zero;
			progressFillRect.anchorMax = new Vector2(0f, 1f);
			progressFillRect.pivot = new Vector2(0f, 0.5f);
			progressFillRect.anchoredPosition = Vector2.zero;
			progressFillRect.sizeDelta = Vector2.zero;

			// Version row
			var version = AddText("Version", parent, "v 1.0  ·  WIN  ·  ANDROID  ·  MADE FOR SPEAKERS-TO-BE", 18, TextAnchor.MiddleCenter, AppDesign.InkFaint);
			version.font = AppDesign.MonoFont;
			var vrt = version.rectTransform;
			vrt.anchorMin = new Vector2(0.5f, 0f);
			vrt.anchorMax = new Vector2(0.5f, 0f);
			vrt.pivot = new Vector2(0.5f, 0f);
			vrt.anchoredPosition = new Vector2(0f, 36f);
			vrt.sizeDelta = new Vector2(1100f, 22f);
		}

		// ---- Builders --------------------------------------------------------

		GameObject AddSolid(string objectName, Transform parent, Color color)
		{
			var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = color;
			return go;
		}

		GameObject AddRounded(string objectName, Transform parent, Color color, float radius)
		{
			var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiRoundedRect));
			go.transform.SetParent(parent, false);
			var g = go.GetComponent<UiRoundedRect>();
			g.color = color;
			g.Radius = radius;
			return go;
		}

		GameObject AddCircle(string objectName, Transform parent, Color color)
		{
			var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiCircle));
			go.transform.SetParent(parent, false);
			go.GetComponent<UiCircle>().color = color;
			return go;
		}

		T AddGraphic<T>(string objectName, Transform parent) where T : Graphic
		{
			var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
			go.transform.SetParent(parent, false);
			return go.GetComponent<T>();
		}

		Text AddText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
		{
			var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
			go.transform.SetParent(parent, false);
			var t = go.GetComponent<Text>();
			t.text = value;
			t.font = AppDesign.BodyFont;
			t.fontSize = fontSize;
			t.alignment = alignment;
			t.color = color;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.verticalOverflow = VerticalWrapMode.Overflow;
			return t;
		}

		void FillParent(RectTransform rt)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = Vector2.zero;
			rt.sizeDelta = Vector2.zero;
		}
	}
}
