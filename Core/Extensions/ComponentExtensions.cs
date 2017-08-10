using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DTMediaCapture.Internal {
	public static class ComponentExtensions {
		// PRAGMA MARK - Public Interface
		public static T GetOrAddComponent<T>(this Component c) where T : UnityEngine.Component {
			var component = c.gameObject.GetComponent<T>();
			if (component == null) {
				component = c.gameObject.AddComponent<T>();
			}
			return component;
		}
	}
}