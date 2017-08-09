using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

using DTMediaCapture.Internal;

namespace DTMediaCapture {
	public class Screenshotter : MonoBehaviour {
		// PRAGMA MARK - Internal
		[Header("Properties")]
		[SerializeField]
		private string screenshotPath_ = "${DESKTOP}/Screenshots";
		[SerializeField]
		private string screenshotNameFormat_ = "Screenshot__${DATE}__${INDEX}";

		[Space]
		[SerializeField]
		private KeyCode togglePauseKey_ = KeyCode.O;
		[SerializeField]
		private KeyCode screenshotKey_ = KeyCode.L;

		private bool paused_ = false;
		private float oldTimeScale_ = 1.0f;

		#if DT_DEBUG_MENU
		private DTDebugMenu.IDynamicGroup dynamicGroup_;
		#endif

		private void Awake() {
			bool debug = Debug.isDebugBuild;
			#if DEBUG
			debug = true;
			#endif

			if (!debug) {
				this.enabled = false;
				return;
			}

			#if DT_DEBUG_MENU
			var inspector = DTDebugMenu.GenericInspectorRegistry.Get("DTMediaCapture");
			inspector.BeginDynamic();
			inspector.RegisterHeader("Screenshotter");
			inspector.RegisterButton("Toggle Paused TimeScale", TogglePausedTimeScale);
			inspector.RegisterButton("Capture Screenshot", CaptureScreenshot);
			dynamicGroup_ = inspector.EndDynamic();
			#endif
		}

		private void OnEnable() {
			#if DT_DEBUG_MENU
			dynamicGroup_.Enabled = true;
			#endif
		}

		private void OnDisable() {
			#if DT_DEBUG_MENU
			dynamicGroup_.Enabled = false;
			#endif
		}

		private void Update() {
			if (Input.GetKeyDown(togglePauseKey_)) {
				TogglePausedTimeScale();
			}

			if (Input.GetKeyDown(screenshotKey_)) {
				CaptureScreenshot();
			}
		}

		private void TogglePausedTimeScale() {
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

		private void CaptureScreenshot() {
			string screenshotPath = screenshotPath_;
			screenshotPath = SavePathUtil.PopulateDesktopVariable(screenshotPath);

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