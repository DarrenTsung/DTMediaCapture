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
		[SerializeField, Range(1, 60)]
		private int frameRate_ = 30;

		[Space]
		[SerializeField]
		private KeyCode toggleRecordingKey_ = KeyCode.K;

		private string populatedRecordingPath_;

		private bool recording_ = false;
		private int frameCount_ = -1;

		private string currentRecordingName_;

		private void Awake() {
			if (!Debug.isDebugBuild) {
				this.enabled = false;
				return;
			}

			// populate recording path
			populatedRecordingPath_ = recordingPath_;
			populatedRecordingPath_ = SavePathUtil.PopulateDesktopVariable(populatedRecordingPath_);
		}

		private void Update() {
			if (Input.GetKeyDown(toggleRecordingKey_)) {
				recording_ = !recording_;
				if (recording_) {
					frameCount_ = -1;
					Time.captureFramerate = frameRate_;
					RefreshSequencePath();
					Debug.Log("Starting Recording!");
				} else {
					Time.captureFramerate = 0;
#if UNITY_EDITOR
					CreateVideoFromCurrentSequence();
#endif

					currentRecordingName_ = null;
					Debug.Log("Finished Recording!");
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
				string currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName);

				if (!Directory.Exists(currentRecordingPath)) {
					finalRecordingName = currentRecordingName;
					break;
				}
				index++;
			}

			Directory.CreateDirectory(Path.Combine(populatedRecordingPath_, finalRecordingName));
			currentRecordingName_ = finalRecordingName;
		}

		private void CreateVideoFromCurrentSequence() {
#if UNITY_EDITOR
			if (string.IsNullOrEmpty(currentRecordingName_)) {
				Debug.LogWarning("Cannot create video because no current recording name!");
				return;
			}

			string binPath = ScriptableObjectEditorUtil.PathForScriptableObjectType<BinMarker>();
			string pathToProject = Application.dataPath.Replace("Assets", "");
			string binFullPath = Path.Combine(pathToProject, binPath);
			string ffmpegPath = Path.Combine(binFullPath, "ffmpeg/ffmpeg");

			string arguments = string.Format("-f image2 -r {0} -i ./{1}/Frame%06d.png -c:v libx264 -r {0} -b:v 30M -pix_fmt yuv420p {1}.mp4 -loglevel debug", frameRate_, currentRecordingName_);

			var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = ffmpegPath;
			process.StartInfo.Arguments = arguments;
			process.StartInfo.WorkingDirectory = populatedRecordingPath_;
			process.Start();

			process.WaitForExit(60 * 1000); // 60 seconds max

			Directory.Delete(Path.Combine(populatedRecordingPath_, currentRecordingName_), recursive: true);
#endif
		}
	}
}