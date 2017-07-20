using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DTMediaCapture.Internal {
	public static class SavePathUtil {
		// PRAGMA MARK - Public Interface
		public static string PopulateDesktopVariable(string path) {
			return path.Replace("${DESKTOP}", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
		}
	}
}