using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AppUI.Stt;
using AvatarSDK.MetaPerson.OculusLipSync;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppUI
{
	public class MainPanelSceneController : MonoBehaviour
	{
		[System.Serializable]
		public class SidebarItem
		{
			public string id;
			public string name;
		}

		[SerializeField] string loginSceneName = "LoginScene";
		[SerializeField] string virtualAssistSceneName = "VirtualAssistScene";
		[SerializeField] string serverBaseUrl = "http://localhost:3000";
		[SerializeField] int maxRecordingSeconds = 20;
		[SerializeField] bool enableOfflineWhisperStt = true;
		[SerializeField] string whisperTinyModelRelativePath = "Whisper/ggml-tiny.bin";
		[SerializeField] bool enableOfflinePiperTts = true;
		[SerializeField] string piperExecutableRelativePath = "Piper/piper.exe";
		[SerializeField] string piperModelRelativePath = "Piper/en_US-hfc_male-medium.onnx";
		[SerializeField] string piperOutputRelativePath = "PiperOutput/answer.wav";
		[SerializeField] int piperTimeoutSeconds = 30;
		[SerializeField] SidebarItem[] topicItems =
		{
			new SidebarItem { id = "family", name = "Family" },
			new SidebarItem { id = "work", name = "Work" },
			new SidebarItem { id = "restaurant", name = "Restaurant" }
		};

		Transform contentRoot;
		Text titleText;
		Text bodyText;
		RawImage virtualAssistView;
		RenderTexture virtualAssistTexture;
		bool virtualAssistLoaded;
		string selectedTopicId = "family";
		string selectedTopicName = "Family";
		string recognizedQuestion = string.Empty;
		bool recording;
		bool addTextInProgress;
		bool ttsInProgress;
		bool transcribing;
		AudioClip recordedClip;
		string selectedMicrophoneDevice;
		WhisperTinySttService whisperSttService;
		Text transcriptText;
		Text answerText;
		Text recordButtonText;
		GameObject voicePanelObject;
		readonly List<GameObject> pageCards = new();
		readonly Dictionary<string, SidebarButtonView> sidebarButtons = new();

		class SidebarButtonView
		{
			public Graphic background;
			public Text label;
			public GameObject activeBorder;
		}

		[System.Serializable]
		class AddTextRequest
		{
			public string topic;
			public string question;
		}

		[System.Serializable]
		class AddTextResponse
		{
			public int id;
			public string answer;
			public string topic;
			public int qeustion_id;
		}

		void Awake()
		{
			whisperSttService = new WhisperTinySttService();
			BuildUi();
			ShowHomeContent();
		}

		void BuildUi()
		{
			var canvas = CreateCanvas();

			var background = CreatePanel("MainPanel Background", canvas.transform, AppDesign.Background);
			SetFullScreen(background.GetComponent<RectTransform>());

			var sidebar = CreatePanel("Sidebar", background.transform, AppDesign.Background);
			var sidebarRect = sidebar.GetComponent<RectTransform>();
			sidebarRect.anchorMin = new Vector2(0f, 0f);
			sidebarRect.anchorMax = new Vector2(0f, 1f);
			sidebarRect.pivot = new Vector2(0f, 0.5f);
			sidebarRect.anchoredPosition = Vector2.zero;
			sidebarRect.sizeDelta = new Vector2(280f, 0f);

			var sidebarBorder = CreatePanel("Sidebar Border", sidebar.transform, AppDesign.SurfaceAlt);
			var sidebarBorderRect = sidebarBorder.GetComponent<RectTransform>();
			sidebarBorderRect.anchorMin = new Vector2(1f, 0f);
			sidebarBorderRect.anchorMax = new Vector2(1f, 1f);
			sidebarBorderRect.pivot = new Vector2(1f, 0.5f);
			sidebarBorderRect.anchoredPosition = Vector2.zero;
			sidebarBorderRect.sizeDelta = new Vector2(1f, 0f);

			var brand = CreateText("Brand", sidebar.transform, "FreeTalk", 28, TextAnchor.MiddleLeft, AppDesign.Ink);
			var brandRect = brand.rectTransform;
			brandRect.anchorMin = new Vector2(0f, 1f);
			brandRect.anchorMax = new Vector2(1f, 1f);
			brandRect.pivot = new Vector2(0.5f, 1f);
			brandRect.anchoredPosition = new Vector2(24f, -28f);
			brandRect.sizeDelta = new Vector2(-48f, 54f);

			float sidebarY = -112f;
			CreateSidebarButton(sidebar.transform, "home", "Home", new Vector2(0f, sidebarY), ShowHomeContent);
			sidebarY -= 44f;
			CreateSidebarButton(sidebar.transform, "scenarios", "Scenarios", new Vector2(0f, sidebarY), ShowScenariosContent);
			sidebarY -= 44f;
			CreateSidebarButton(sidebar.transform, "course", "My course", new Vector2(0f, sidebarY), ShowCourseContent);
			sidebarY -= 44f;
			CreateSidebarButton(sidebar.transform, "progress", "Progress", new Vector2(0f, sidebarY), ShowProgressContent);
			sidebarY -= 44f;
			CreateSidebarButton(sidebar.transform, "settings", "Settings", new Vector2(0f, sidebarY), ShowSettingsContent);
			sidebarY -= 66f;
			CreateSidebarActionButton(sidebar.transform, "Quick talk", new Vector2(0f, sidebarY), ShowQuickTalkContent);
			sidebarY -= 72f;
			CreateSidebarButton(sidebar.transform, "logout", "Logout", new Vector2(0f, sidebarY), Logout);

			var content = CreatePanel("Content Area", background.transform, AppDesign.Surface);
			var contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 0f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.offsetMin = new Vector2(320f, 48f);
			contentRect.offsetMax = new Vector2(-48f, -48f);
			contentRoot = content.transform;

			titleText = CreateText("Content Title", contentRoot, string.Empty, 36, TextAnchor.MiddleLeft, AppDesign.Ink);
			var titleRect = titleText.rectTransform;
			titleRect.anchorMin = new Vector2(0f, 1f);
			titleRect.anchorMax = new Vector2(1f, 1f);
			titleRect.pivot = new Vector2(0.5f, 1f);
			titleRect.anchoredPosition = new Vector2(36f, -34f);
			titleRect.sizeDelta = new Vector2(-72f, 60f);

			bodyText = CreateText("Content Body", contentRoot, string.Empty, 20, TextAnchor.UpperLeft, AppDesign.InkSoft);
			var bodyRect = bodyText.rectTransform;
			bodyRect.anchorMin = new Vector2(0f, 1f);
			bodyRect.anchorMax = new Vector2(1f, 1f);
			bodyRect.pivot = new Vector2(0.5f, 1f);
			bodyRect.anchoredPosition = new Vector2(36f, -112f);
			bodyRect.sizeDelta = new Vector2(-72f, 96f);

			virtualAssistView = CreateVirtualAssistView(contentRoot);
			CreateVoicePanel(contentRoot);
			SetAssistantVisible(false);
		}

		void ShowHomeContent()
		{
			SetActiveSidebarItem("home");
			ClearPageCards();
			SetAssistantVisible(false);
			titleText.text = "Hi, Jamie";
			bodyText.text = "ready for a chat?";
			CreatePageCard("Today’s session", "Quick talk with Maya\nIntermediate · 10 min\nChoose a topic and start speaking.", new Vector2(36f, -170f), new Vector2(470f, 160f), AppDesign.Accent);
			CreatePageCard("Day streak", "3 days\nKeep your speaking habit alive.", new Vector2(530f, -170f), new Vector2(260f, 160f), AppDesign.Accent2);
			CreatePageCard("Continue your course", "Airport check-in · 7 min · A2\nOrder at a cafe · 6 min · A2\nLost in a new city · 5 min · A2", new Vector2(36f, -350f), new Vector2(470f, 190f), AppDesign.Good);
			CreatePageCard("Browse topics", "Family\nWork\nRestaurant", new Vector2(530f, -350f), new Vector2(260f, 190f), AppDesign.Accent);
		}

		void ShowScenariosContent()
		{
			SetActiveSidebarItem("scenarios");
			ClearPageCards();
			SetAssistantVisible(false);
			titleText.text = "Scenarios";
			bodyText.text = "Pick a real-life roleplay and practice with confidence.";
			CreatePageCard("Airport check-in", "Find your gate, handle a flight delay.\n7 min · A2", new Vector2(36f, -170f), new Vector2(360f, 150f), AppDesign.Accent);
			CreatePageCard("Lost in a new city", "Ask for directions, read a map.\n5 min · A2", new Vector2(420f, -170f), new Vector2(360f, 150f), AppDesign.Good);
			CreatePageCard("Order at a cafe", "Off-menu requests, dietary swaps.\n6 min · A2", new Vector2(36f, -340f), new Vector2(360f, 150f), AppDesign.Accent2);
			CreatePageCard("Job interview", "Walk me through your resume.\n12 min · B2", new Vector2(420f, -340f), new Vector2(360f, 150f), AppDesign.Accent);
		}

		void ShowTopicContent(SidebarItem topicItem)
		{
			SetActiveSidebarItem(topicItem.id == "quick-talk" ? string.Empty : "course");
			selectedTopicId = topicItem.id;
			selectedTopicName = topicItem.name;
			ClearPageCards();
			SetAssistantVisible(true);
			titleText.text = $"Topic: {topicItem.name}";
			bodyText.text = string.Empty;

			if (!virtualAssistLoaded)
			{
				StartCoroutine(LoadVirtualAssistIntoContent());
			}
		}

		void ShowQuickTalkContent()
		{
			ShowTopicContent(new SidebarItem { id = "quick-talk", name = "Quick talk" });
		}

		void ShowCourseContent()
		{
			SetActiveSidebarItem("course");
			ClearPageCards();
			SetAssistantVisible(false);
			titleText.text = "Build your course";
			bodyText.text = "Choose a speaking course. Each course opens the assistant with the matching topic.";

			string[] descriptions =
			{
				"Small talk, home life, and everyday family questions.\n10 min · A2/B1",
				"Meetings, interviews, tasks, and office conversation.\n12 min · B1/B2",
				"Ordering, reservations, requests, and service phrases.\n8 min · A2/B1"
			};
			Color[] accents = { AppDesign.Accent, AppDesign.Good, AppDesign.Accent2 };
			for (int i = 0; i < topicItems.Length; i++)
			{
				float x = 36f + i * 264f;
				string description = i < descriptions.Length ? descriptions[i] : "Practice this course with your AI speaking assistant.";
				Color accent = i < accents.Length ? accents[i] : AppDesign.Accent;
				CreateCourseTopicCard(topicItems[i], description, new Vector2(x, -170f), accent);
			}

			CreatePageCard("Conversation style", "Realistic\nMatch real-world cadence, accents, and slang.\n\nClean\nTextbook clarity. Slow and structured.\n\nPlayful\nGames, jokes, and role-play scenarios.", new Vector2(36f, -360f), new Vector2(360f, 220f), AppDesign.Accent);
			CreatePageCard("Daily goal", "15 min per day\n\nShort, consistent practice.\n~1.8 hrs / week", new Vector2(420f, -360f), new Vector2(360f, 220f), AppDesign.Accent2);
			CreatePageCard("Next 4 weeks", "1. Foundations: small talk & introductions\n2. Travel core: arrivals, navigation, food\n3. Business basics: meetings & email-speak\n4. Mock interview week", new Vector2(36f, -610f), new Vector2(744f, 190f), AppDesign.Good);
		}

		void ShowProgressContent()
		{
			SetActiveSidebarItem("progress");
			ClearPageCards();
			SetAssistantVisible(false);
			titleText.text = "Progress";
			bodyText.text = "Track minutes, skills, and badges.";
			CreatePageCard("Weekly minutes", "42 min\n+18% this week", new Vector2(36f, -170f), new Vector2(230f, 140f), AppDesign.Accent);
			CreatePageCard("Day streak", "3 days\nKeep going", new Vector2(286f, -170f), new Vector2(230f, 140f), AppDesign.Accent2);
			CreatePageCard("Words spoken", "1,248\nconversation words", new Vector2(536f, -170f), new Vector2(230f, 140f), AppDesign.Good);
			CreatePageCard("Skills", "Pronunciation 72%\nGrammar 68%\nFluency 74%\nVocabulary 70%", new Vector2(36f, -335f), new Vector2(360f, 190f), AppDesign.Accent);
			CreatePageCard("Badges", "First chat\nQuick talk\nThree-day streak", new Vector2(420f, -335f), new Vector2(346f, 190f), AppDesign.Accent2);
		}

		void ShowSettingsContent()
		{
			SetActiveSidebarItem("settings");
			ClearPageCards();
			SetAssistantVisible(false);
			titleText.text = "Settings";
			bodyText.text = "Manage your profile, voice setup, and account details.";
			CreatePageCard("Profile", "Edit your learner name, level, and speaking goal.", new Vector2(36f, -170f), new Vector2(360f, 160f), AppDesign.Accent);
			CreatePageCard("Audio", "Microphone, Whisper STT, Piper TTS, and avatar speech playback settings.", new Vector2(420f, -170f), new Vector2(360f, 160f), AppDesign.Accent2);
			CreatePageCard("Account", "Sign-in information and local app data.", new Vector2(36f, -360f), new Vector2(360f, 160f), AppDesign.Good);
			CreatePageCard("Tutor persona", "Maya · Friendly best friend\nWarm, encouraging, casual conversation.", new Vector2(420f, -360f), new Vector2(360f, 160f), AppDesign.Accent);
		}

		void SetAssistantVisible(bool isVisible)
		{
			if (virtualAssistView != null)
			{
				virtualAssistView.gameObject.SetActive(isVisible);
			}

			if (voicePanelObject != null)
			{
				voicePanelObject.SetActive(isVisible);
			}
		}

		void ClearPageCards()
		{
			foreach (var pageCard in pageCards)
			{
				if (pageCard != null)
				{
					Destroy(pageCard);
				}
			}

			pageCards.Clear();
		}

		void CreatePageCard(string heading, string body, Vector2 anchoredPosition, Vector2 size, Color accentColor)
		{
			var card = CreatePanel($"{heading} Card", contentRoot, AppDesign.SurfaceAlt);
			pageCards.Add(card);

			var rect = card.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = size;

			var accent = CreatePanel("Accent", card.transform, accentColor);
			var accentRect = accent.GetComponent<RectTransform>();
			accentRect.anchorMin = new Vector2(0f, 1f);
			accentRect.anchorMax = new Vector2(0f, 1f);
			accentRect.pivot = new Vector2(0f, 1f);
			accentRect.anchoredPosition = new Vector2(18f, -18f);
			accentRect.sizeDelta = new Vector2(44f, 6f);

			var headingText = CreateText("Heading", card.transform, heading, 22, TextAnchor.UpperLeft, AppDesign.Ink);
			var headingRect = headingText.rectTransform;
			headingRect.anchorMin = new Vector2(0f, 1f);
			headingRect.anchorMax = new Vector2(1f, 1f);
			headingRect.pivot = new Vector2(0.5f, 1f);
			headingRect.anchoredPosition = new Vector2(18f, -34f);
			headingRect.sizeDelta = new Vector2(-36f, 34f);

			var bodyTextCard = CreateText("Body", card.transform, body, 16, TextAnchor.UpperLeft, AppDesign.InkSoft);
			var bodyRect = bodyTextCard.rectTransform;
			bodyRect.anchorMin = Vector2.zero;
			bodyRect.anchorMax = Vector2.one;
			bodyRect.pivot = new Vector2(0.5f, 0.5f);
			bodyRect.offsetMin = new Vector2(18f, 18f);
			bodyRect.offsetMax = new Vector2(-18f, -78f);
		}

		void CreateCourseTopicCard(SidebarItem topicItem, string body, Vector2 anchoredPosition, Color accentColor)
		{
			if (topicItem == null)
			{
				return;
			}

			var card = CreatePanel($"{topicItem.name} Course Card", contentRoot, AppDesign.SurfaceAlt);
			pageCards.Add(card);

			var rect = card.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(240f, 160f);

			var button = card.AddComponent<Button>();
			button.targetGraphic = card.GetComponent<Image>();
			var selectedTopic = topicItem;
			button.onClick.AddListener(() => ShowTopicContent(selectedTopic));

			var icon = CreatePanel("Icon", card.transform, accentColor);
			var iconRect = icon.GetComponent<RectTransform>();
			iconRect.anchorMin = new Vector2(0f, 1f);
			iconRect.anchorMax = new Vector2(0f, 1f);
			iconRect.pivot = new Vector2(0f, 1f);
			iconRect.anchoredPosition = new Vector2(18f, -18f);
			iconRect.sizeDelta = new Vector2(42f, 42f);

			var iconText = CreateText("Icon Text", icon.transform, topicItem.name.Substring(0, 1).ToUpperInvariant(), 18, TextAnchor.MiddleCenter, Color.white);
			SetFullScreen(iconText.rectTransform);

			var headingText = CreateText("Heading", card.transform, topicItem.name, 19, TextAnchor.UpperLeft, AppDesign.Ink);
			var headingRect = headingText.rectTransform;
			headingRect.anchorMin = new Vector2(0f, 1f);
			headingRect.anchorMax = new Vector2(1f, 1f);
			headingRect.pivot = new Vector2(0.5f, 1f);
			headingRect.anchoredPosition = new Vector2(18f, -70f);
			headingRect.sizeDelta = new Vector2(-36f, 28f);

			var bodyTextCard = CreateText("Body", card.transform, body, 14, TextAnchor.UpperLeft, AppDesign.InkSoft);
			var bodyRect = bodyTextCard.rectTransform;
			bodyRect.anchorMin = Vector2.zero;
			bodyRect.anchorMax = Vector2.one;
			bodyRect.pivot = new Vector2(0.5f, 0.5f);
			bodyRect.offsetMin = new Vector2(18f, 18f);
			bodyRect.offsetMax = new Vector2(-18f, -100f);
		}

		IEnumerator LoadVirtualAssistIntoContent()
		{
			virtualAssistLoaded = true;

			var mainAudioListener = Camera.main != null ? Camera.main.GetComponent<AudioListener>() : null;
			if (mainAudioListener != null)
			{
				mainAudioListener.enabled = false;
			}

			if (AttachPreloadedVirtualAssist())
			{
				yield break;
			}

			var loadOperation = SceneManager.LoadSceneAsync(virtualAssistSceneName, LoadSceneMode.Additive);
			while (loadOperation != null && !loadOperation.isDone)
			{
				yield return null;
			}

			var virtualScene = SceneManager.GetSceneByName(virtualAssistSceneName);
			foreach (var root in virtualScene.GetRootGameObjects())
			{
				foreach (var sceneCamera in root.GetComponentsInChildren<Camera>(true))
				{
					sceneCamera.targetTexture = virtualAssistTexture;
					sceneCamera.aspect = 16f / 9f;
				}
			}
		}

		bool AttachPreloadedVirtualAssist()
		{
			if (!AvatarPreloadService.IsPreloaded || AvatarPreloadService.Cameras.Count == 0)
			{
				return false;
			}

			AvatarPreloadService.SetCamerasActive(true, virtualAssistTexture);

			return true;
		}

		void Logout()
		{
			AvatarPreloadService.SetCamerasActive(false);
			SceneManager.LoadScene(loginSceneName);
		}

		Canvas CreateCanvas()
		{
			var canvasObject = new GameObject("Main Panel Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			var canvas = canvasObject.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			return canvas;
		}

		void CreateVoicePanel(Transform parent)
		{
			voicePanelObject = CreatePanel("Voice STT Panel", parent, AppDesign.SurfaceAlt);
			var rect = voicePanelObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(1f, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.anchoredPosition = new Vector2(0f, 34f);
			rect.sizeDelta = new Vector2(-72f, 190f);

			transcriptText = CreateText("Transcript Text", voicePanelObject.transform, "Transcript: ", 18, TextAnchor.UpperLeft, AppDesign.Ink);
			var transcriptRect = transcriptText.rectTransform;
			transcriptRect.anchorMin = new Vector2(0f, 1f);
			transcriptRect.anchorMax = new Vector2(1f, 1f);
			transcriptRect.pivot = new Vector2(0.5f, 1f);
			transcriptRect.anchoredPosition = new Vector2(18f, -16f);
			transcriptRect.sizeDelta = new Vector2(-36f, 34f);

			answerText = CreateText("Answer Text", voicePanelObject.transform, "Answer: ", 18, TextAnchor.UpperLeft, AppDesign.InkSoft);
			var answerRect = answerText.rectTransform;
			answerRect.anchorMin = new Vector2(0f, 1f);
			answerRect.anchorMax = new Vector2(1f, 1f);
			answerRect.pivot = new Vector2(0.5f, 1f);
			answerRect.anchoredPosition = new Vector2(18f, -60f);
			answerRect.sizeDelta = new Vector2(-36f, 74f);

			CreateVoiceButton(voicePanelObject.transform, "Start Recording", new Vector2(18f, 18f), ToggleRecording, out recordButtonText);
		}

		void ToggleRecording()
		{
			if (recording)
			{
				StopRecording();
			}
			else
			{
				StartRecording();
			}
		}

		void StartRecording()
		{
			if (transcribing || addTextInProgress || ttsInProgress)
			{
				transcriptText.text = "Transcript: please wait until the current request finishes.";
				return;
			}

			if (Microphone.devices.Length == 0)
			{
				transcriptText.text = "Transcript: no microphone was found.";
				return;
			}

			recording = true;
			recognizedQuestion = string.Empty;
			selectedMicrophoneDevice = Microphone.devices[0];
			transcriptText.text = "Transcript: listening...";
			recordButtonText.text = "Stop Recording";

			recordedClip = Microphone.Start(selectedMicrophoneDevice, false, maxRecordingSeconds, 16000);
			transcriptText.text = enableOfflineWhisperStt
				? "Transcript: recording for offline Whisper tiny STT..."
				: "Transcript: local STT is disabled.";
		}

		void StopRecording()
		{
			recording = false;
			recordButtonText.text = "Start Recording";

			int recordedSamples = Microphone.IsRecording(selectedMicrophoneDevice)
				? Microphone.GetPosition(selectedMicrophoneDevice)
				: 0;

			if (Microphone.IsRecording(selectedMicrophoneDevice))
			{
				Microphone.End(selectedMicrophoneDevice);
			}

			if (recordedClip != null && recordedSamples > 0)
			{
				recordedClip = TrimRecordedClip(recordedClip, recordedSamples);
			}

			if (string.IsNullOrWhiteSpace(recognizedQuestion))
			{
				if (enableOfflineWhisperStt && recordedClip != null)
				{
					if (!HasAudibleSignal(recordedClip))
					{
						transcriptText.text = "Transcript: blank audio. Please speak closer to the microphone or check input permissions.";
						return;
					}

					StartCoroutine(TranscribeRecordedClipWithWhisper());
				}
				else
				{
					transcriptText.text = "Transcript: no speech recognized.";
				}
			}
		}

		AudioClip TrimRecordedClip(AudioClip sourceClip, int recordedSamples)
		{
			int sampleCount = Mathf.Clamp(recordedSamples, 0, sourceClip.samples);
			if (sampleCount <= 0 || sampleCount == sourceClip.samples)
			{
				return sourceClip;
			}

			float[] sourceData = new float[sourceClip.samples * sourceClip.channels];
			sourceClip.GetData(sourceData, 0);

			float[] trimmedData = new float[sampleCount * sourceClip.channels];
			System.Array.Copy(sourceData, trimmedData, trimmedData.Length);

			var trimmedClip = AudioClip.Create(
				"Trimmed Whisper Recording",
				sampleCount,
				sourceClip.channels,
				sourceClip.frequency,
				false);
			trimmedClip.SetData(trimmedData, 0);
			return trimmedClip;
		}

		bool HasAudibleSignal(AudioClip audioClip)
		{
			float[] samples = new float[audioClip.samples * audioClip.channels];
			audioClip.GetData(samples, 0);

			float sumSquares = 0f;
			for (int i = 0; i < samples.Length; i++)
			{
				sumSquares += samples[i] * samples[i];
			}

			float rms = Mathf.Sqrt(sumSquares / Mathf.Max(1, samples.Length));
			return rms > 0.001f;
		}

		IEnumerator TranscribeRecordedClipWithWhisper()
		{
			if (transcribing)
			{
				yield break;
			}

			transcribing = true;
			transcriptText.text = "Transcript: transcribing with Whisper tiny...";

			bool success = false;
			string transcribedText = string.Empty;
			string error = string.Empty;
			yield return whisperSttService.Transcribe(recordedClip, whisperTinyModelRelativePath, (result, text, message) =>
			{
				success = result;
				transcribedText = text;
				error = message;
			});
			transcribing = false;

			if (!success)
			{
				transcriptText.text = $"Transcript: {error}";
				yield break;
			}

			recognizedQuestion = transcribedText;
			transcriptText.text = $"Transcript: {recognizedQuestion}";
			yield return SendAddTextRequest(selectedTopicId, recognizedQuestion);
		}

		IEnumerator SendAddTextRequest(string topic, string question)
		{
			addTextInProgress = true;
			answerText.text = "Answer: waiting for server...";

			var requestBody = new AddTextRequest
			{
				topic = topic,
				question = question
			};
			byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));

			using var request = new UnityWebRequest(BuildApiUrl("/add_text"), UnityWebRequest.kHttpVerbPOST);
			request.uploadHandler = new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Accept", "application/json");

			yield return request.SendWebRequest();

			addTextInProgress = false;
			if (request.result != UnityWebRequest.Result.Success)
			{
				answerText.text = $"Answer: request failed: {request.error}";
				yield break;
			}

			var response = JsonUtility.FromJson<AddTextResponse>(request.downloadHandler.text);
			if (string.IsNullOrWhiteSpace(response.answer))
			{
				answerText.text = "Answer: server returned no answer.";
				yield break;
			}

			answerText.text = $"Answer: {response.answer}";
			if (enableOfflinePiperTts)
			{
				yield return SpeakAnswerWithPiper(response.answer);
			}
		}

		IEnumerator SpeakAnswerWithPiper(string answer)
		{
			ttsInProgress = true;
			answerText.text = $"Answer: {answer}\n\nGenerating speech with Piper...";

			string piperPath = Path.Combine(Application.streamingAssetsPath, piperExecutableRelativePath);
			string modelPath = Path.Combine(Application.streamingAssetsPath, piperModelRelativePath);
			string outputPath = Path.Combine(Application.streamingAssetsPath, piperOutputRelativePath);

			if (!File.Exists(piperPath))
			{
				answerText.text = $"Answer: {answer}\n\nPiper executable not found: {piperPath}";
				ttsInProgress = false;
				yield break;
			}

			if (!File.Exists(modelPath))
			{
				answerText.text = $"Answer: {answer}\n\nPiper model not found: {modelPath}";
				ttsInProgress = false;
				yield break;
			}

			if (!ValidatePiperRuntime(piperPath, out string runtimeError))
			{
				answerText.text = $"Answer: {answer}\n\n{runtimeError}";
				ttsInProgress = false;
				yield break;
			}

			string outputDirectory = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrEmpty(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}

			if (File.Exists(outputPath))
			{
				File.Delete(outputPath);
			}

			string processError = string.Empty;
			var process = new System.Diagnostics.Process
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo
				{
					FileName = piperPath,
					Arguments = $"--model \"{modelPath}\" --output_file \"{outputPath}\"",
					WorkingDirectory = Path.GetDirectoryName(piperPath),
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				},
				EnableRaisingEvents = true
			};

			try
			{
				process.Start();
				process.StandardInput.WriteLine(answer);
				process.StandardInput.Close();
			}
			catch (Exception exception)
			{
				answerText.text = $"Answer: {answer}\n\nPiper failed to start: {exception.Message}";
				process.Dispose();
				ttsInProgress = false;
				yield break;
			}

			float startedAt = Time.realtimeSinceStartup;
			while (!process.HasExited)
			{
				if (Time.realtimeSinceStartup - startedAt > piperTimeoutSeconds)
				{
					process.Kill();
					answerText.text = $"Answer: {answer}\n\nPiper timed out.";
					process.Dispose();
					ttsInProgress = false;
					yield break;
				}

				yield return null;
			}

			processError = process.StandardError.ReadToEnd();
			int exitCode = process.ExitCode;
			process.Dispose();

			if (exitCode != 0 || !File.Exists(outputPath))
			{
				string message = exitCode == -1073741515
					? "Piper failed because required Windows DLL files are missing. Extract the full Piper Windows release into StreamingAssets/Piper, not only piper.exe."
					: $"Piper failed with exit code {exitCode}: {processError}";
				answerText.text = $"Answer: {answer}\n\n{message}";
				ttsInProgress = false;
				yield break;
			}

			yield return LoadAndPlayGeneratedSpeech(answer, outputPath);
			ttsInProgress = false;
		}

		IEnumerator LoadAndPlayGeneratedSpeech(string answer, string outputPath)
		{
			string audioUri = new Uri(outputPath).AbsoluteUri;
			using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioUri, AudioType.WAV);
			yield return audioRequest.SendWebRequest();

			if (audioRequest.result != UnityWebRequest.Result.Success)
			{
				answerText.text = $"Answer: {answer}\n\nCould not load Piper WAV: {audioRequest.error}";
				yield break;
			}

			var speechClip = DownloadHandlerAudioClip.GetContent(audioRequest);
			var sceneHandler = FindVirtualAssistSceneHandler();
			if (sceneHandler == null || !sceneHandler.PlaySpeechClip(speechClip))
			{
				answerText.text = $"Answer: {answer}\n\nSpeech was generated, but the avatar audio source was not found.";
				yield break;
			}

			answerText.text = $"Answer: {answer}\n\nSpeaking...";
		}

		bool ValidatePiperRuntime(string piperPath, out string error)
		{
			error = string.Empty;
			string piperDirectory = Path.GetDirectoryName(piperPath);
			if (string.IsNullOrEmpty(piperDirectory))
			{
				error = "Piper directory was not found.";
				return false;
			}

			bool hasNativeLibraries = Directory.GetFiles(piperDirectory, "*.dll").Length > 0;
			if (!hasNativeLibraries)
			{
				error = "Piper runtime files are missing. Extract the full Piper Windows release into Assets/StreamingAssets/Piper, including all .dll files and espeak-ng-data.";
				return false;
			}

			return true;
		}

		OculusSampleSceneHandler FindVirtualAssistSceneHandler()
		{
			var preloadedHandler = AvatarPreloadService.GetSceneHandler();
			if (preloadedHandler != null)
			{
				return preloadedHandler;
			}

			var virtualScene = SceneManager.GetSceneByName(virtualAssistSceneName);
			if (virtualScene.IsValid())
			{
				foreach (var root in virtualScene.GetRootGameObjects())
				{
					var handler = root.GetComponentInChildren<OculusSampleSceneHandler>(true);
					if (handler != null)
					{
						return handler;
					}
				}
			}

			return FindObjectOfType<OculusSampleSceneHandler>(true);
		}

		string BuildApiUrl(string path)
		{
			return $"{serverBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
		}

		float CreateSidebarSection(Transform parent, string sectionName, string itemPrefix, SidebarItem[] items, float startY, System.Action<SidebarItem> onItemClicked)
		{
			if (!string.IsNullOrWhiteSpace(sectionName))
			{
				CreateSidebarSectionTitle(parent, sectionName, new Vector2(0f, startY));
			}

			float currentY = string.IsNullOrWhiteSpace(sectionName) ? startY : startY - 34f;

			foreach (var item in items)
			{
				var sidebarItem = item;
				CreateSidebarButton(parent, $"{itemPrefix}:{sidebarItem.id}", sidebarItem.name, new Vector2(0f, currentY), () => onItemClicked(sidebarItem), 32f);
				currentY -= 44f;
			}

			return currentY;
		}

		void CreateSidebarSectionTitle(Transform parent, string label, Vector2 anchoredPosition)
		{
			var title = CreateText($"{label} Title", parent, label.ToUpperInvariant(), 11, TextAnchor.MiddleLeft, AppDesign.InkFaint);
			var rect = title.rectTransform;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(-48f, 24f);
			rect.offsetMin = new Vector2(24f, rect.offsetMin.y);
		}

		void SetActiveSidebarItem(string activeId)
		{
			foreach (var entry in sidebarButtons)
			{
				bool isActive = entry.Key == activeId;
				entry.Value.background.color = isActive ? AppDesign.AccentSoft : Color.clear;
				entry.Value.label.color = isActive ? AppDesign.AccentInk : AppDesign.InkSoft;
				entry.Value.label.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
				entry.Value.activeBorder.SetActive(isActive);
			}
		}

		void CreateSidebarButton(Transform parent, string id, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, float leftPadding = 18f)
		{
			var buttonObject = CreateRoundedPanel($"{label} Button", parent, Color.clear, 10f);
			var rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(-40f, 40f);

			var button = buttonObject.AddComponent<Button>();
			button.targetGraphic = buttonObject.GetComponent<Graphic>();
			button.onClick.AddListener(onClick);

			var activeBorder = CreateRoundedPanel("Active Border", buttonObject.transform, AppDesign.Accent, 2f);
			var borderRect = activeBorder.GetComponent<RectTransform>();
			borderRect.anchorMin = new Vector2(0f, 0f);
			borderRect.anchorMax = new Vector2(0f, 1f);
			borderRect.pivot = new Vector2(0f, 0.5f);
			borderRect.anchoredPosition = Vector2.zero;
			borderRect.sizeDelta = new Vector2(3f, 0f);
			activeBorder.SetActive(false);

			var text = CreateText("Text", buttonObject.transform, label, 15, TextAnchor.MiddleLeft, AppDesign.InkSoft);
			SetFullScreen(text.rectTransform);
			text.rectTransform.offsetMin = new Vector2(leftPadding + 10f, 0f);
			text.rectTransform.offsetMax = new Vector2(-14f, 0f);

			sidebarButtons[id] = new SidebarButtonView
			{
				background = buttonObject.GetComponent<Graphic>(),
				label = text,
				activeBorder = activeBorder
			};
		}

		void CreateSidebarActionButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
		{
			var buttonObject = CreateRoundedPanel($"{label} Action Button", parent, AppDesign.Accent, 12f);
			var rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(-40f, 46f);

			var button = buttonObject.AddComponent<Button>();
			button.targetGraphic = buttonObject.GetComponent<Graphic>();
			button.onClick.AddListener(onClick);

			var text = CreateText("Text", buttonObject.transform, label, 15, TextAnchor.MiddleCenter, Color.white);
			text.fontStyle = FontStyle.Bold;
			SetFullScreen(text.rectTransform);
		}

		RawImage CreateVirtualAssistView(Transform parent)
		{
			virtualAssistTexture = new RenderTexture(1280, 720, 24)
			{
				name = "Virtual Assist Content Texture"
			};

			var viewObject = new GameObject("Virtual Assist Content View", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
			viewObject.transform.SetParent(parent, false);

			var rect = viewObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.offsetMin = new Vector2(36f, 250f);
			rect.offsetMax = new Vector2(-36f, -220f);

			var rawImage = viewObject.GetComponent<RawImage>();
			rawImage.texture = virtualAssistTexture;
			rawImage.color = Color.white;

			var aspectFitter = viewObject.GetComponent<AspectRatioFitter>();
			aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			aspectFitter.aspectRatio = 16f / 9f;

			return rawImage;
		}

		Button CreateVoiceButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, out Text buttonText)
		{
			var buttonObject = CreatePanel($"{label} Button", parent, AppDesign.Accent);
			var rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(0f, 0f);
			rect.pivot = new Vector2(0f, 0f);
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = new Vector2(170f, 44f);

			var button = buttonObject.AddComponent<Button>();
			button.targetGraphic = buttonObject.GetComponent<Image>();
			button.onClick.AddListener(onClick);

			buttonText = CreateText("Text", buttonObject.transform, label, 17, TextAnchor.MiddleCenter, AppDesign.Ink);
			SetFullScreen(buttonText.rectTransform);
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
}
