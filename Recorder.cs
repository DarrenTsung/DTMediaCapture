using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using DTMediaCapture.Internal;

namespace DTMediaCapture {
	// Based off keijiro's ImageSequenceOut (https://github.com/keijiro/ImageSequenceOut)
	public class Recorder : MonoBehaviour {
		// PRAGMA MARK - Internal
		[Header("Properties")]
		[SerializeField]
		private string recordingPath_ = "${DESKTOP}/Recordings";
		[SerializeField]
		private string recordingNameFormat_ = "Recording__${DATE}__${INDEX}";

		[Space]
		#pragma warning disable 0414 // not used because used inside !UNITY_EDITOR
		[SerializeField]
		private string nonEditorFfmpegPath_ = "${DESKTOP}/Ffmpeg/ffmpeg";
		#pragma warning restore 0414

		[Space]
		[SerializeField, Range(1, 60)]
		private int frameRate_ = 30;

		[Header("Key-Bindings")]
		[SerializeField]
		private bool useKeyBindings_ = true;
		[Space]
		[SerializeField]
		private KeyCode toggleRecordingKey_ = KeyCode.K;

		[Header("Record Set Length")]
		[SerializeField]
		private float recordSetLengthInSeconds_ = 10.0f;
		[SerializeField]
		private bool recordSetLengthOnStart_ = false;

		[Header("Read-Only")]
		[SerializeField]
		private float recordingTime_ = 0.0f;

		private string populatedRecordingPath_;

		private bool recording_ = false;
		private int frameCount_ = -1;

		private string currentRecordingName_;

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

			// populate recording path
			populatedRecordingPath_ = recordingPath_;
			populatedRecordingPath_ = SavePathUtil.PopulateDesktopVariable(populatedRecordingPath_);

			#if DT_DEBUG_MENU
			var inspector = DTDebugMenu.GenericInspectorRegistry.Get("DTMediaCapture");
			inspector.BeginDynamic();
			inspector.RegisterHeader("Recorder");
			inspector.RegisterToggle("Set Recording: " + (useKeyBindings_ ? string.Format("({0})", toggleRecordingKey_) : ""), (b) => { if (b) { StartRecording(); } else { StopRecording(); }}, () => recording_);
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

		private void Start() {
			if (recordSetLengthOnStart_) {
				StartRecording();
			}
		}

		private void StartRecording() {
			if (recording_) {
				Debug.LogWarning("Can't start recording when already recording!");
				return;
			}

			recording_ = true;
			frameCount_ = -1;
			Time.captureFramerate = frameRate_;
			RefreshSequencePath();
			Debug.Log("Starting Recording!");
		}

		private void StopRecording() {
			if (!recording_) {
				Debug.LogWarning("Can't stop recording when not recording!");
				return;
			}

			recording_ = false;
			Time.captureFramerate = 0;
			CreateVideoFromCurrentSequence();

			var currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName_);
			Debug.Log("Finished Recording! Saved video at: " + currentRecordingPath + "!");
			currentRecordingName_ = null;
		}

		private void Update() {
			if (useKeyBindings_) {
				if (Input.GetKeyDown(toggleRecordingKey_)) {
					if (recording_) {
						StopRecording();
					} else {
						StartRecording();
					}
				}
			}

			UpdateRecording();
		}

		private void UpdateRecording() {
			if (!recording_) {
				return;
			}

			if (string.IsNullOrEmpty(currentRecordingName_)) {
				return;
			}

			if (frameCount_ > 0) {
				var currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName_);
				var screenshotPath = Path.Combine(currentRecordingPath, "Frame" + frameCount_.ToString("000000") + ".png");
				Application.CaptureScreenshot(screenshotPath);
			}
			frameCount_++;

			recordingTime_ = (float)frameCount_ / (float)frameRate_;
			if (recordSetLengthOnStart_ && recordingTime_ > recordSetLengthInSeconds_) {
				StopRecording();
			}
		}

		private void RefreshSequencePath() {
			string recordingNameFormat = recordingNameFormat_;
			recordingNameFormat = SavePathUtil.PopulateDateVariable(recordingNameFormat);

			if (!recordingNameFormat.Contains("${INDEX}")) {
				Debug.LogWarning("RecordingNameFormat is missing ${INDEX} - adding _${INDEX} to the end!");
				recordingNameFormat = recordingNameFormat + "_${INDEX}";
			}

			string finalRecordingName = null;
			int index = 0;
			while (true) {
				string currentRecordingName  = recordingNameFormat.Replace("${INDEX}", index.ToString());
				string currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName) + ".mp4";

				if (!File.Exists(currentRecordingPath)) {
					finalRecordingName = currentRecordingName;
					break;
				}
				index++;
			}

			Directory.CreateDirectory(Path.Combine(populatedRecordingPath_, finalRecordingName));
			currentRecordingName_ = finalRecordingName;
		}

		private void CreateVideoFromCurrentSequence() {
			if (string.IsNullOrEmpty(currentRecordingName_)) {
				Debug.LogWarning("Cannot create video because no current recording name!");
				return;
			}

			string ffmpegPath = "";
#if UNITY_EDITOR
			string binPath = ScriptableObjectEditorUtil.PathForScriptableObjectType<BinMarker>();
			string pathToProject = Application.dataPath.Replace("Assets", "");
			string binFullPath = Path.Combine(pathToProject, binPath);
			ffmpegPath = Path.Combine(binFullPath, "ffmpeg/ffmpeg");
#else
			ffmpegPath = SavePathUtil.PopulateDesktopVariable(nonEditorFfmpegPath_);
#endif

			string arguments = string.Format("-f image2 -r {0} -i ./{1}/Frame%06d.png -c:v libx264 -r {0} -b:v 30M -pix_fmt yuv420p {1}.mp4 -loglevel debug", frameRate_, currentRecordingName_);

			var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = ffmpegPath;
			process.StartInfo.Arguments = arguments;
			process.StartInfo.WorkingDirectory = populatedRecordingPath_;
			process.Start();

			process.WaitForExit(5 * 60 * 1000); // 5 minutes max

			Directory.Delete(Path.Combine(populatedRecordingPath_, currentRecordingName_), recursive: true);
		}
	}
}