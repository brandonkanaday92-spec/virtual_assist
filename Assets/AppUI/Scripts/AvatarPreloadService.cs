using System.Collections.Generic;
using AvatarSDK.MetaPerson.OculusLipSync;
using UnityEngine;

namespace AppUI
{
	public static class AvatarPreloadService
	{
		public static bool IsPreloading { get; private set; }
		public static bool IsPreloaded { get; private set; }
		public static bool Failed { get; private set; }
		public static string Error { get; private set; } = string.Empty;
		public static readonly List<GameObject> Roots = new();
		public static readonly List<Camera> Cameras = new();

		public static void Begin()
		{
			IsPreloading = true;
			IsPreloaded = false;
			Failed = false;
			Error = string.Empty;
			Roots.Clear();
			Cameras.Clear();
		}

		public static void Complete(IEnumerable<GameObject> roots)
		{
			Roots.Clear();
			Cameras.Clear();

			foreach (var root in roots)
			{
				if (root == null)
				{
					continue;
				}

				Roots.Add(root);
				foreach (var sceneCamera in root.GetComponentsInChildren<Camera>(true))
				{
					Cameras.Add(sceneCamera);
				}
			}

			IsPreloading = false;
			IsPreloaded = true;
			Failed = false;
			Error = string.Empty;
		}

		public static void Fail(string error)
		{
			IsPreloading = false;
			IsPreloaded = false;
			Failed = true;
			Error = error;
		}

		public static OculusSampleSceneHandler GetSceneHandler()
		{
			foreach (var root in Roots)
			{
				if (root == null)
				{
					continue;
				}

				var handler = root.GetComponentInChildren<OculusSampleSceneHandler>(true);
				if (handler != null)
				{
					return handler;
				}
			}

			return Object.FindObjectOfType<OculusSampleSceneHandler>(true);
		}

		public static void SetCamerasActive(bool isActive, RenderTexture targetTexture = null)
		{
			foreach (var sceneCamera in Cameras)
			{
				if (sceneCamera == null)
				{
					continue;
				}

				sceneCamera.enabled = isActive;
				sceneCamera.targetTexture = isActive ? targetTexture : null;
				sceneCamera.aspect = 16f / 9f;

				var audioListener = sceneCamera.GetComponent<AudioListener>();
				if (audioListener != null)
				{
					audioListener.enabled = isActive;
				}
			}
		}
	}
}
