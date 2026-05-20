using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace AppUI.Stt
{
	public class WhisperTinySttService
	{
		object whisperManager;
		Type whisperManagerType;

		public IEnumerator Transcribe(AudioClip audioClip, string modelRelativePath, Action<bool, string, string> completed)
		{
			if (audioClip == null)
			{
				completed(false, string.Empty, "No recorded audio clip is available.");
				yield break;
			}

			string modelPath = Path.Combine(Application.streamingAssetsPath, modelRelativePath);
			if (!File.Exists(modelPath))
			{
				completed(false, string.Empty, $"Whisper tiny model was not found: {modelPath}");
				yield break;
			}

			if (!EnsureWhisperManager(modelRelativePath, out string setupError))
			{
				completed(false, string.Empty, setupError);
				yield break;
			}

			MethodInfo transcribeMethod = FindTranscribeMethod(whisperManagerType);
			if (transcribeMethod == null)
			{
				completed(false, string.Empty, "Installed Whisper plugin does not expose GetTextAsync.");
				yield break;
			}

			object taskObject;
			try
			{
				taskObject = InvokeTranscribeMethod(transcribeMethod, audioClip);
			}
			catch (Exception exception)
			{
				completed(false, string.Empty, exception.InnerException?.Message ?? exception.Message);
				yield break;
			}

			if (taskObject is not Task task)
			{
				completed(false, string.Empty, "Whisper transcription did not return an async task.");
				yield break;
			}

			while (!task.IsCompleted)
			{
				yield return null;
			}

			if (task.IsFaulted)
			{
				completed(false, string.Empty, task.Exception?.GetBaseException().Message ?? "Whisper transcription failed.");
				yield break;
			}

			string text = ExtractTextFromTask(task);
			if (string.Equals(text?.Trim(), "BLANK_AUDIO", StringComparison.OrdinalIgnoreCase))
			{
				completed(false, string.Empty, "Whisper detected blank audio. Please speak clearly into the microphone.");
				yield break;
			}

			if (string.IsNullOrWhiteSpace(text))
			{
				completed(false, string.Empty, "Whisper returned no recognized text.");
				yield break;
			}

			completed(true, text.Trim(), string.Empty);
		}

		bool EnsureWhisperManager(string modelRelativePath, out string error)
		{
			error = string.Empty;
			if (whisperManager != null)
			{
				return true;
			}

			whisperManagerType = FindType("Whisper.WhisperManager") ?? FindType("WhisperManager");
			if (whisperManagerType == null)
			{
				error = "Whisper Unity plugin is not installed. Add com.whisper.unity through Package Manager.";
				return false;
			}

			if (typeof(Component).IsAssignableFrom(whisperManagerType))
			{
				var managerObject = new GameObject("Whisper Tiny STT Service");
				managerObject.SetActive(false);
				UnityEngine.Object.DontDestroyOnLoad(managerObject);
				whisperManager = managerObject.AddComponent(whisperManagerType);
				ConfigureStringMember(whisperManager, modelRelativePath);
				ConfigureBoolMember(whisperManager, "IsModelPathInStreamingAssets", "isModelPathInStreamingAssets", true);
				managerObject.SetActive(true);
			}
			else
			{
				whisperManager = Activator.CreateInstance(whisperManagerType);
				ConfigureStringMember(whisperManager, modelRelativePath);
			}

			return true;
		}

		Type FindType(string typeName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assembly.GetType(typeName);
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}

		void ConfigureStringMember(object target, string modelPath)
		{
			string[] memberNames = { "ModelPath", "modelPath", "Model", "model", "ModelName", "modelName" };
			foreach (string memberName in memberNames)
			{
				var property = target.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.CanWrite && property.PropertyType == typeof(string))
				{
					property.SetValue(target, modelPath);
					return;
				}

				var field = target.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null && field.FieldType == typeof(string))
				{
					field.SetValue(target, modelPath);
					return;
				}
			}
		}

		void ConfigureBoolMember(object target, string propertyName, string fieldName, bool value)
		{
			var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
			{
				property.SetValue(target, value);
				return;
			}

			var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null && field.FieldType == typeof(bool))
			{
				field.SetValue(target, value);
			}
		}

		MethodInfo FindTranscribeMethod(Type managerType)
		{
			foreach (var method in managerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (method.Name == "GetTextAsync" && typeof(Task).IsAssignableFrom(method.ReturnType))
				{
					return method;
				}
			}

			return null;
		}

		object InvokeTranscribeMethod(MethodInfo method, AudioClip audioClip)
		{
			var parameters = method.GetParameters();
			if (parameters.Length == 3)
			{
				float[] samples = new float[audioClip.samples * audioClip.channels];
				audioClip.GetData(samples, 0);
				return method.Invoke(whisperManager, new object[] { samples, audioClip.frequency, audioClip.channels });
			}

			if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AudioClip))
			{
				return method.Invoke(whisperManager, new object[] { audioClip });
			}

			throw new MissingMethodException("Supported GetTextAsync overload was not found.");
		}

		string ExtractTextFromTask(Task task)
		{
			object result = task.GetType().GetProperty("Result")?.GetValue(task);
			if (result == null)
			{
				return string.Empty;
			}

			if (result is string text)
			{
				return text;
			}

			return result.GetType().GetProperty("Result")?.GetValue(result)?.ToString()
				?? result.GetType().GetProperty("Text")?.GetValue(result)?.ToString()
				?? result.GetType().GetField("Result")?.GetValue(result)?.ToString()
				?? result.GetType().GetField("Text")?.GetValue(result)?.ToString()
				?? string.Empty;
		}
	}
}
