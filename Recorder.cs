using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

using DTMediaCapture.Internal;

namespace DTMediaCapture {
	// Based off keijiro's ImageSequenceOut (https://github.com/keijiro/ImageSequenceOut)
	public class Recorder : MonoBehaviour {
		// PRAGMA MARK - Internal
		[Header("Properties")]
		[SerializeField]
		private string sequencePathFormat_ = "${DESKTOP}/Recordings/${DATE}_${INDEX}";

		[Space]
		[SerializeField, Range(1, 60)]
		private int frameRate_ = 30;

		[Space]
		[SerializeField]
		private KeyCode toggleRecordingKey_ = KeyCode.K;

		private bool recording_ = false;
		private int frameCount_ = -1;

		private string currentSequencePath_;

		private void Awake() {
			if (!Debug.isDebugBuild) {
				this.enabled = false;
				return;
			}
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
					currentSequencePath_ = null;
					Debug.Log("Ending Recording!");
				}
			}

			UpdateRecording();
		}

		private void UpdateRecording() {
			if (!recording_) {
				return;
			}

			if (string.IsNullOrEmpty(currentSequencePath_)) {
				return;
			}

			if (frameCount_ > 0) {
				var screenshotPath = Path.Combine(currentSequencePath_, "Frame" + frameCount_.ToString("000000") + ".png");
				Application.CaptureScreenshot(screenshotPath);
			}
			frameCount_++;
		}

		private void RefreshSequencePath() {
			string sequencePath = sequencePathFormat_;
			sequencePath = SavePathUtil.PopulateDesktopVariable(sequencePath);

			if (!sequencePath.Contains("${INDEX}")) {
				Debug.LogWarning("SequencePath is missing ${INDEX} - adding _${INDEX} to the end!");
				sequencePath = sequencePath + "_${INDEX}";
			}

			sequencePath = SavePathUtil.PopulateDateVariable(sequencePath);

			string finalSequencePath = null;
			int index = 0;
			while (true) {
				string currentSequencePath = sequencePath.Replace("${INDEX}", index.ToString());

				if (!Directory.Exists(currentSequencePath)) {
					finalSequencePath = currentSequencePath;
					break;
				}
				index++;
			}

			Directory.CreateDirectory(finalSequencePath);
			currentSequencePath_ = finalSequencePath;
		}
	}
}