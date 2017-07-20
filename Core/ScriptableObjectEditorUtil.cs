#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DTMediaCapture.Internal {
	public static class ScriptableObjectEditorUtil {
		public static Dictionary<Type, string> cachedPaths_ = new Dictionary<Type, string>();
		public static string PathForScriptableObjectType<T>() where T : ScriptableObject {
			Type type = typeof(T);
			if (cachedPaths_.ContainsKey(type)) {
				return cachedPaths_[type];
			}

			T instance = ScriptableObject.CreateInstance<T>();
			MonoScript script = MonoScript.FromScriptableObject(instance);
			string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(script));
			ScriptableObject.DestroyImmediate(instance);

			cachedPaths_[type] = path;
			return path;
		}
	}
}
#endif