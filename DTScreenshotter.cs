using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DTScreenshotter {
	public class DTScreenshotter : MonoBehaviour {
		// PRAGMA MARK - Internal
		[Header("Properties")]
		[SerializeField]
		private string screenshotPath_ = "${DESKTOP}/Screenshots";
		[SerializeField]
		private string screenshotNameFormat_ = "Screenshot__${DATE}_${INDEX}";

		[Space]
		[SerializeField]
		private KeyCode togglePauseKey_ = KeyCode.O;
		[SerializeField]
		private KeyCode screenshotKey_ = KeyCode.P;

		private bool paused_ = false;
		private float oldTimeScale_ = 1.0f;

		private void Awake() {
			if (!Debug.isDebugBuild) {
				this.enabled = false;
				return;
			}
		}

		private void Update() {
			if (Input.GetKeyDown(togglePauseKey_)) {
				if (!paused_) {
					oldTimeScale_ = Time.timeScale;
				}
				paused_ = !paused_;
				if (paused_) {
					Time.timeScale = 0.0f;
				} else {
					// NOTE (darren): prevent case where paused when timeScale == 0.0f
					Time.timeScale = Mathf.Approximately(oldTimeScale_, 0.0f) ? 1.0f : oldTimeScale_;
				}
			}

			if (Input.GetKeyDown(screenshotKey_)) {
				CaptureScreenshot();
			}
		}

		private void CaptureScreenshot() {
			string screenshotPath = screenshotPath_;
			screenshotPath = screenshotPath.Replace("${DESKTOP}", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

			string screenshotName = Path.GetFileNameWithoutExtension(screenshotNameFormat_);
			if (!screenshotName.Contains("${INDEX}")) {
				Debug.LogWarning("ScreenshotNameFormat is missing ${INDEX} - adding _${INDEX} to the end!");
				screenshotName = screenshotName + "_${INDEX}";
			}

			screenshotName = screenshotName.Replace("${DATE}", System.DateTime.Now.ToString("MM-dd-yyyy"));

			string finalScreenshotPath = null;
			int index = 0;
			while (true) {
				string currentScreenshotName = screenshotName.Replace("${INDEX}", index.ToString()) + ".png";
				string currentScreenshotPath = Path.Combine(screenshotPath, currentScreenshotName);

				if (!File.Exists(currentScreenshotPath)) {
					finalScreenshotPath = currentScreenshotPath;
					break;
				}
				index++;
			}

			string finalScreenshotDirectoryPath = Path.GetDirectoryName(finalScreenshotPath);
			if (!Directory.Exists(finalScreenshotDirectoryPath)) {
				Directory.CreateDirectory(finalScreenshotDirectoryPath);
			}

			Application.CaptureScreenshot(finalScreenshotPath);
			Debug.Log("Saved screenshot at: " + finalScreenshotPath + "!");
		}
	}
}