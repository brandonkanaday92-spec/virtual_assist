using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppUI
{
	public class AuthSceneController : MonoBehaviour
	{
		[SerializeField] bool registerMode;
		[SerializeField] string serverBaseUrl = "http://localhost:3000";
		[SerializeField] string loginSceneName = "LoginScene";
		[SerializeField] string registerSceneName = "RegisterScene";
		[SerializeField] string appSceneName = "MainPanelScene";

		const float AuthSidePanelWidth = 560f;
		const float AuthFormContentWidth = 420f;
		const float AuthFieldHeight = 56f;
		const float AuthButtonHeight = 56f;

		Text messageText;
		InputField usernameInput;
		InputField passwordInput;
		Dropdown genderDropdown;
		bool loginInProgress;
		bool registerInProgress;

		[Serializable]
		class LoginRequest
		{
			public string username;
			public string password;
		}

		[Serializable]
		class RegisterRequest
		{
			public string username;
			public string password;
			public string gender;
		}

		[Serializable]
		class AuthResponse
		{
			public string message;
			public AuthUser user;
		}

		[Serializable]
		class AuthUser
		{
			public string username;
			public string gender;
		}

		void Awake()
		{
			BuildSceneUi();
		}

		void BuildSceneUi()
		{
			var canvas = CreateCanvas();
			var background = CreatePanel("Background", canvas.transform, AppDesign.Accent);
			SetFullScreen(background.GetComponent<RectTransform>());

			CreateAuthSidePanel(background.transform);

			var formArea = CreatePanel("Auth Form Area", background.transform, AppDesign.Background);
			var formAreaRect = formArea.GetComponent<RectTransform>();
			formAreaRect.anchorMin = new Vector2(0f, 0f);
			formAreaRect.anchorMax = new Vector2(1f, 1f);
			formAreaRect.offsetMin = new Vector2(AuthSidePanelWidth, 0f);
			formAreaRect.offsetMax = Vector2.zero;

			var card = new GameObject("Auth Form", typeof(RectTransform));
			card.transform.SetParent(formArea.transform, false);
			var cardRect = card.GetComponent<RectTransform>();
			cardRect.anchorMin = new Vector2(0.5f, 0.5f);
			cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.pivot = new Vector2(0.5f, 0.5f);
			cardRect.anchoredPosition = Vector2.zero;
			cardRect.sizeDelta = new Vector2(AuthFormContentWidth, registerMode ? 600f : 500f);

			CreateTitle(card.transform, registerMode ? "Make a FreeTalk account" : "Sign in to FreeTalk");

			usernameInput = CreateLabeledInput(card.transform, registerMode ? "User ID (your unique login name)" : "User ID", "Your login ID", new Vector2(0f, registerMode ? 126f : 78f), false);
			passwordInput = CreateLabeledInput(card.transform, "Password", "Password", new Vector2(0f, registerMode ? 38f : -10f), true);

			if (registerMode)
			{
				CreateFieldLabel(card.transform, "Gender", new Vector2(0f, -6f));
				genderDropdown = CreateDropdown(card.transform, new Vector2(0f, -50f));
				CreateButton(card.transform, "Create account", new Vector2(0f, -116f), AppDesign.Accent, Register);
				CreateButton(card.transform, "Back to Login", new Vector2(0f, -178f), AppDesign.SurfaceAlt, OpenLogin);
			}
			else
			{
				CreateButton(card.transform, "Sign in", new Vector2(0f, -92f), AppDesign.Accent, Login);
				CreateButton(card.transform, "Create an account", new Vector2(0f, -160f), AppDesign.SurfaceAlt, OpenRegister);
			}

			messageText = CreateText("Message", card.transform, string.Empty, 16, TextAnchor.MiddleCenter, AppDesign.InkSoft);
			var messageRect = messageText.rectTransform;
			messageRect.anchorMin = new Vector2(0f, 0f);
			messageRect.anchorMax = new Vector2(1f, 0f);
			messageRect.anchoredPosition = new Vector2(0f, 26f);
			messageRect.sizeDelta = new Vector2(-56f, 28f);
		}

		void Login()
		{
			if (loginInProgress)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(usernameInput.text) || string.IsNullOrWhiteSpace(passwordInput.text))
			{
				messageText.text = "Enter username and password.";
				return;
			}

			StartCoroutine(LoginRequestCoroutine(usernameInput.text.Trim(), passwordInput.text));
		}

		IEnumerator LoginRequestCoroutine(string username, string password)
		{
			loginInProgress = true;
			messageText.text = "Logging in...";

			var requestBody = new LoginRequest
			{
				username = username,
				password = password
			};
			byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));

			using var request = new UnityWebRequest(BuildApiUrl("/login"), UnityWebRequest.kHttpVerbPOST);
			request.uploadHandler = new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Accept", "application/json");

			yield return request.SendWebRequest();

			loginInProgress = false;
			if (request.result != UnityWebRequest.Result.Success)
			{
				messageText.text = $"Login failed: {request.error}";
				yield break;
			}

			AuthResponse response;
			try
			{
				response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
			}
			catch (Exception exception)
			{
				messageText.text = "Login failed: invalid server response.";
				Debug.LogError(exception);
				yield break;
			}

			if (response?.user == null || string.IsNullOrWhiteSpace(response.user.username))
			{
				messageText.text = string.IsNullOrWhiteSpace(response?.message) ? "Login failed." : response.message;
				yield break;
			}

			PlayerPrefs.SetString("username", response.user.username);
			PlayerPrefs.SetString("gender", response.user.gender);
			PlayerPrefs.Save();
			SceneManager.LoadScene(appSceneName);
		}

		string BuildApiUrl(string path)
		{
			return $"{serverBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
		}

		void Register()
		{
			if (registerInProgress)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(usernameInput.text) || string.IsNullOrWhiteSpace(passwordInput.text) || genderDropdown.value == 0)
			{
				messageText.text = "Enter username, password, and gender.";
				return;
			}

			StartCoroutine(RegisterRequestCoroutine(usernameInput.text.Trim(), passwordInput.text, genderDropdown.options[genderDropdown.value].text.ToLowerInvariant()));
		}

		IEnumerator RegisterRequestCoroutine(string username, string password, string gender)
		{
			registerInProgress = true;
			messageText.text = "Registering...";

			var requestBody = new RegisterRequest
			{
				username = username,
				password = password,
				gender = gender
			};
			byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));

			using var request = new UnityWebRequest(BuildApiUrl("/register"), UnityWebRequest.kHttpVerbPOST);
			request.uploadHandler = new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Accept", "application/json");

			yield return request.SendWebRequest();

			registerInProgress = false;
			if (request.result != UnityWebRequest.Result.Success)
			{
				messageText.text = $"Register failed: {request.error}";
				yield break;
			}

			AuthResponse response;
			try
			{
				response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
			}
			catch (Exception exception)
			{
				messageText.text = "Register failed: invalid server response.";
				Debug.LogError(exception);
				yield break;
			}

			if (response?.user == null || string.IsNullOrWhiteSpace(response.user.username))
			{
				messageText.text = string.IsNullOrWhiteSpace(response?.message) ? "Register failed." : response.message;
				yield break;
			}

			messageText.text = string.IsNullOrWhiteSpace(response.message) ? "Registration complete." : response.message;
			SceneManager.LoadScene(loginSceneName);
		}

		void OpenLogin()
		{
			SceneManager.LoadScene(loginSceneName);
		}

		void OpenRegister()
		{
			SceneManager.LoadScene(registerSceneName);
		}

		Canvas CreateCanvas()
		{
			var canvasObject = new GameObject("Auth Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			var canvas = canvasObject.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			return canvas;
		}

		void CreateAuthSidePanel(Transform parent)
		{
			var sidePanel = CreateGradientPanel("Auth Side Panel", parent);
			var rect = sidePanel.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 0.5f);
			rect.anchoredPosition = Vector2.zero;
			rect.sizeDelta = new Vector2(AuthSidePanelWidth, 0f);

			CreateDecorCircle(sidePanel.transform, "Top Circle", new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -120f), new Vector2(320f, 320f), 0.10f);
			CreateDecorCircle(sidePanel.transform, "Bottom Circle", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(-80f, -140f), new Vector2(280f, 280f), 0.06f);
			CreateDecorCircle(sidePanel.transform, "Middle Circle", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(140f, 140f), 0.08f);

			var logo = CreatePanel("Logo Mark", sidePanel.transform, Color.white);
			var logoRect = logo.GetComponent<RectTransform>();
			logoRect.anchorMin = new Vector2(0f, 1f);
			logoRect.anchorMax = new Vector2(0f, 1f);
			logoRect.pivot = new Vector2(0f, 1f);
			logoRect.anchoredPosition = new Vector2(36f, -38f);
			logoRect.sizeDelta = new Vector2(42f, 42f);

			var logoText = CreateText("Logo Text", logo.transform, "F", 26, TextAnchor.MiddleCenter, AppDesign.Accent);
			logoText.font = AppDesign.LogoFont;
			logoText.fontStyle = FontStyle.Italic;
			SetFullScreen(logoText.rectTransform);

			var wordmark = CreateText("Wordmark", sidePanel.transform, "FreeTalk", 24, TextAnchor.MiddleLeft, Color.white);
			wordmark.font = AppDesign.LogoFont;
			var wordmarkRect = wordmark.rectTransform;
			wordmarkRect.anchorMin = new Vector2(0f, 1f);
			wordmarkRect.anchorMax = new Vector2(1f, 1f);
			wordmarkRect.pivot = new Vector2(0.5f, 1f);
			wordmarkRect.anchoredPosition = new Vector2(92f, -39f);
			wordmarkRect.sizeDelta = new Vector2(-128f, 42f);

			var headline = CreateText("Side Headline", sidePanel.transform, "Open your mouth.\nFind your voice.", 52, TextAnchor.MiddleLeft, Color.white);
			headline.font = AppDesign.LogoFont;
			var headlineRect = headline.rectTransform;
			headlineRect.anchorMin = new Vector2(0f, 0.5f);
			headlineRect.anchorMax = new Vector2(1f, 0.5f);
			headlineRect.pivot = new Vector2(0.5f, 0.5f);
			headlineRect.anchoredPosition = new Vector2(44f, 54f);
			headlineRect.sizeDelta = new Vector2(-88f, 170f);

			var subcopy = CreateText("Side Copy", sidePanel.transform, "Practice real English conversations with a patient AI tutor.", 16, TextAnchor.UpperLeft, AppDesign.AccentInk);
			var subcopyRect = subcopy.rectTransform;
			subcopyRect.anchorMin = new Vector2(0f, 0.5f);
			subcopyRect.anchorMax = new Vector2(1f, 0.5f);
			subcopyRect.pivot = new Vector2(0.5f, 0.5f);
			subcopyRect.anchoredPosition = new Vector2(44f, -74f);
			subcopyRect.sizeDelta = new Vector2(-88f, 70f);

			var footer = CreateText("Side Footer", sidePanel.transform, "Windows / Android / English", 12, TextAnchor.MiddleLeft, AppDesign.AccentInk);
			var footerRect = footer.rectTransform;
			footerRect.anchorMin = new Vector2(0f, 0f);
			footerRect.anchorMax = new Vector2(1f, 0f);
			footerRect.pivot = new Vector2(0.5f, 0f);
			footerRect.anchoredPosition = new Vector2(36f, 34f);
			footerRect.sizeDelta = new Vector2(-72f, 28f);
		}

		void CreateTitle(Transform parent, string title)
		{
			var titleText = CreateText("Title", parent, title, 38, TextAnchor.MiddleLeft, AppDesign.Ink);
			titleText.font = AppDesign.LogoFont;
			var rect = titleText.rectTransform;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -58f);
			rect.sizeDelta = new Vector2(0f, 54f);
		}

		void CreateKicker(Transform parent, string value)
		{
			var kicker = CreateText("Kicker", parent, value.ToUpperInvariant(), 11, TextAnchor.MiddleLeft, AppDesign.InkFaint);
			var rect = kicker.rectTransform;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -58f);
			rect.sizeDelta = new Vector2(0f, 24f);
		}

		void CreateSubtitle(Transform parent, string value)
		{
			var subtitle = CreateText("Subtitle", parent, value, 14, TextAnchor.MiddleLeft, AppDesign.InkSoft);
			var rect = subtitle.rectTransform;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -108f);
			rect.sizeDelta = new Vector2(0f, 28f);
		}

		InputField CreateLabeledInput(Transform parent, string label, string placeholder, Vector2 anchoredPosition, bool isPassword)
		{
			CreateFieldLabel(parent, label, new Vector2(anchoredPosition.x, anchoredPosition.y + 44f));
			return CreateInput(parent, placeholder, anchoredPosition, isPassword);
		}

		void CreateFieldLabel(Transform parent, string label, Vector2 anchoredPosition)
		{
			var labelText = CreateText($"{label} Label", parent, label, 12, TextAnchor.MiddleLeft, AppDesign.InkSoft);
			var rect = labelText.rectTransform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(AuthFormContentWidth, 20f);
		}

		void CreateRememberRow(Transform parent, Vector2 anchoredPosition)
		{
			var remember = CreateText("Remember Me", parent, "Remember me", 13, TextAnchor.MiddleLeft, AppDesign.InkSoft);
			var rememberRect = remember.rectTransform;
			rememberRect.anchorMin = new Vector2(0.5f, 0.5f);
			rememberRect.anchorMax = new Vector2(0.5f, 0.5f);
			rememberRect.pivot = new Vector2(0.5f, 0.5f);
			rememberRect.anchoredPosition = new Vector2(anchoredPosition.x - 80f, anchoredPosition.y);
			rememberRect.sizeDelta = new Vector2(180f, 24f);

			var forgot = CreateText("Forgot Password", parent, "Forgot password?", 13, TextAnchor.MiddleRight, AppDesign.Accent);
			var forgotRect = forgot.rectTransform;
			forgotRect.anchorMin = new Vector2(0.5f, 0.5f);
			forgotRect.anchorMax = new Vector2(0.5f, 0.5f);
			forgotRect.pivot = new Vector2(0.5f, 0.5f);
			forgotRect.anchoredPosition = new Vector2(anchoredPosition.x + 98f, anchoredPosition.y);
			forgotRect.sizeDelta = new Vector2(160f, 24f);
		}

		InputField CreateInput(Transform parent, string placeholder, Vector2 anchoredPosition, bool isPassword)
		{
			var inputObject = CreateRoundedPanel($"{placeholder} Input", parent, AppDesign.SurfaceAlt, 12f);
			var rect = inputObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(AuthFormContentWidth, AuthFieldHeight);

			var input = inputObject.AddComponent<InputField>();
			input.targetGraphic = inputObject.GetComponent<Graphic>();
			input.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;

			var text = CreateText("Text", inputObject.transform, string.Empty, 17, TextAnchor.MiddleLeft, AppDesign.Ink);
			SetFullScreen(text.rectTransform);
			text.rectTransform.offsetMin = new Vector2(16f, 0f);
			text.rectTransform.offsetMax = new Vector2(-16f, 0f);
			input.textComponent = text;

			var placeholderText = CreateText("Placeholder", inputObject.transform, placeholder, 17, TextAnchor.MiddleLeft, AppDesign.InkFaint);
			SetFullScreen(placeholderText.rectTransform);
			placeholderText.rectTransform.offsetMin = new Vector2(16f, 0f);
			placeholderText.rectTransform.offsetMax = new Vector2(-16f, 0f);
			input.placeholder = placeholderText;

			return input;
		}

		Dropdown CreateDropdown(Transform parent, Vector2 anchoredPosition)
		{
			var dropdownObject = CreateRoundedPanel("Gender Dropdown", parent, AppDesign.SurfaceAlt, 12f);
			var rect = dropdownObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(AuthFormContentWidth, AuthFieldHeight);

			var dropdown = dropdownObject.AddComponent<Dropdown>();
			dropdown.targetGraphic = dropdownObject.GetComponent<Graphic>();
			dropdown.options.Clear();
			dropdown.options.Add(new Dropdown.OptionData("Gender"));
			dropdown.options.Add(new Dropdown.OptionData("Male"));
			dropdown.options.Add(new Dropdown.OptionData("Female"));
			dropdown.options.Add(new Dropdown.OptionData("Other"));

			var label = CreateText("Label", dropdownObject.transform, "Gender", 17, TextAnchor.MiddleLeft, AppDesign.Ink);
			SetFullScreen(label.rectTransform);
			label.rectTransform.offsetMin = new Vector2(16f, 0f);
			label.rectTransform.offsetMax = new Vector2(-16f, 0f);
			dropdown.captionText = label;
			dropdown.template = CreateDropdownTemplate(dropdownObject.transform);

			return dropdown;
		}

		RectTransform CreateDropdownTemplate(Transform parent)
		{
			var template = CreateRoundedPanel("Template", parent, AppDesign.SurfaceAlt, 12f);
			template.SetActive(false);
			var templateRect = template.GetComponent<RectTransform>();
			templateRect.anchorMin = new Vector2(0f, 0f);
			templateRect.anchorMax = new Vector2(1f, 0f);
			templateRect.pivot = new Vector2(0.5f, 1f);
			templateRect.anchoredPosition = new Vector2(0f, -2f);
			templateRect.sizeDelta = new Vector2(0f, 160f);

			var scrollRect = template.AddComponent<ScrollRect>();
			scrollRect.horizontal = false;

			var viewport = CreatePanel("Viewport", template.transform, Color.clear);
			var viewportRect = viewport.GetComponent<RectTransform>();
			SetFullScreen(viewportRect);
			var mask = viewport.AddComponent<Mask>();
			mask.showMaskGraphic = false;
			scrollRect.viewport = viewportRect;

			var content = new GameObject("Content", typeof(RectTransform));
			content.transform.SetParent(viewport.transform, false);
			var contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.anchoredPosition = Vector2.zero;
			contentRect.sizeDelta = new Vector2(0f, 132f);
			scrollRect.content = contentRect;

			var item = CreateRoundedPanel("Item", content.transform, AppDesign.Surface, 8f);
			var itemRect = item.GetComponent<RectTransform>();
			itemRect.anchorMin = new Vector2(0f, 1f);
			itemRect.anchorMax = new Vector2(1f, 1f);
			itemRect.pivot = new Vector2(0.5f, 1f);
			itemRect.anchoredPosition = Vector2.zero;
			itemRect.sizeDelta = new Vector2(0f, 36f);

			var toggle = item.AddComponent<Toggle>();
			toggle.targetGraphic = item.GetComponent<Graphic>();

			var itemLabel = CreateText("Item Label", item.transform, "Option", 18, TextAnchor.MiddleLeft, AppDesign.Ink);
			SetFullScreen(itemLabel.rectTransform);
			itemLabel.rectTransform.offsetMin = new Vector2(14f, 4f);
			itemLabel.rectTransform.offsetMax = new Vector2(-14f, -4f);

			return templateRect;
		}

		Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Color color, UnityEngine.Events.UnityAction onClick)
		{
			var buttonObject = CreateRoundedPanel($"{label} Button", parent, color, 14f);
			var rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(AuthFormContentWidth, AuthButtonHeight);

			var button = buttonObject.AddComponent<Button>();
			button.targetGraphic = buttonObject.GetComponent<Graphic>();
			button.onClick.AddListener(onClick);

			var text = CreateText("Text", buttonObject.transform, label, 16, TextAnchor.MiddleCenter, AppDesign.Ink);
			SetFullScreen(text.rectTransform);
			return button;
		}

		GameObject CreatePanel(string objectName, Transform parent, Color color)
		{
			var panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			panel.transform.SetParent(parent, false);
			panel.GetComponent<Image>().color = color;
			return panel;
		}

		GameObject CreateRoundedPanel(string objectName, Transform parent, Color color, float radius)
		{
			var panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(AuthRoundedRect));
			panel.transform.SetParent(parent, false);
			var graphic = panel.GetComponent<AuthRoundedRect>();
			graphic.color = color;
			graphic.Radius = radius;
			return panel;
		}

		GameObject CreateGradientPanel(string objectName, Transform parent)
		{
			var panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(AuthSidePanelGradient));
			panel.transform.SetParent(parent, false);
			return panel;
		}

		void CreateDecorCircle(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, float opacity)
		{
			var circle = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(AuthDecorCircle));
			circle.transform.SetParent(parent, false);
			var circleGraphic = circle.GetComponent<AuthDecorCircle>();
			circleGraphic.color = new Color(1f, 1f, 1f, opacity);

			var rect = circle.GetComponent<RectTransform>();
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = size;
		}

		Text CreateText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
		{
			var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
			textObject.transform.SetParent(parent, false);
			var text = textObject.GetComponent<Text>();
			text.text = value;
			text.font = AppDesign.BodyFont;
			text.fontSize = fontSize;
			text.alignment = alignment;
			text.color = color;
			return text;
		}

		void SetFullScreen(RectTransform rectTransform)
		{
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.pivot = new Vector2(0.5f, 0.5f);
			rectTransform.anchoredPosition = Vector2.zero;
			rectTransform.sizeDelta = Vector2.zero;
		}
	}

	public class AuthSidePanelGradient : Graphic
	{
		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();

			var rect = GetPixelAdjustedRect();
			var topLeft = new Vector2(rect.xMin, rect.yMax);
			var topRight = new Vector2(rect.xMax, rect.yMax);
			var bottomRight = new Vector2(rect.xMax, rect.yMin);
			var bottomLeft = new Vector2(rect.xMin, rect.yMin);

			AddVert(vh, topLeft, AppDesign.Accent);
			AddVert(vh, topRight, AppDesign.Accent2);
			AddVert(vh, bottomRight, AppDesign.Ink);
			AddVert(vh, bottomLeft, AppDesign.Accent);

			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(2, 3, 0);
		}

		static void AddVert(VertexHelper vh, Vector2 position, Color color)
		{
			var vertex = UIVertex.simpleVert;
			vertex.position = position;
			vertex.color = color;
			vh.AddVert(vertex);
		}
	}

	public class AuthDecorCircle : Graphic
	{
		const int SegmentCount = 48;

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();

			var rect = GetPixelAdjustedRect();
			var center = rect.center;
			var radiusX = rect.width * 0.5f;
			var radiusY = rect.height * 0.5f;

			var centerVertex = UIVertex.simpleVert;
			centerVertex.position = center;
			centerVertex.color = color;
			vh.AddVert(centerVertex);

			for (int i = 0; i <= SegmentCount; i++)
			{
				float angle = i / (float)SegmentCount * Mathf.PI * 2f;
				var vertex = UIVertex.simpleVert;
				vertex.position = new Vector2(center.x + Mathf.Cos(angle) * radiusX, center.y + Mathf.Sin(angle) * radiusY);
				vertex.color = color;
				vh.AddVert(vertex);
			}

			for (int i = 1; i <= SegmentCount; i++)
			{
				vh.AddTriangle(0, i, i + 1);
			}
		}
	}

	public class AuthRoundedRect : Graphic
	{
		const int CornerSegments = 8;

		[SerializeField] float radius = 12f;

		public float Radius
		{
			get => radius;
			set
			{
				radius = Mathf.Max(0f, value);
				SetVerticesDirty();
			}
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();

			var rect = GetPixelAdjustedRect();
			float clampedRadius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
			var center = rect.center;

			AddVertex(vh, center);
			AddCorner(vh, new Vector2(rect.xMax - clampedRadius, rect.yMax - clampedRadius), clampedRadius, 0f, 90f);
			AddCorner(vh, new Vector2(rect.xMin + clampedRadius, rect.yMax - clampedRadius), clampedRadius, 90f, 180f);
			AddCorner(vh, new Vector2(rect.xMin + clampedRadius, rect.yMin + clampedRadius), clampedRadius, 180f, 270f);
			AddCorner(vh, new Vector2(rect.xMax - clampedRadius, rect.yMin + clampedRadius), clampedRadius, 270f, 360f);

			int outerVertexCount = vh.currentVertCount - 1;
			for (int i = 1; i <= outerVertexCount; i++)
			{
				int next = i == outerVertexCount ? 1 : i + 1;
				vh.AddTriangle(0, i, next);
			}
		}

		void AddCorner(VertexHelper vh, Vector2 center, float cornerRadius, float startDegrees, float endDegrees)
		{
			for (int i = 0; i <= CornerSegments; i++)
			{
				float t = i / (float)CornerSegments;
				float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
				AddVertex(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cornerRadius);
			}
		}

		void AddVertex(VertexHelper vh, Vector2 position)
		{
			var vertex = UIVertex.simpleVert;
			vertex.position = position;
			vertex.color = color;
			vh.AddVert(vertex);
		}
	}
}
