﻿#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

[ExecuteInEditMode]
public class MotionEditor : MonoBehaviour {

	public string Folder = string.Empty;
	public File[] Files = new File[0];

	//public bool ShowMotion = false;
	public bool Mirror = false;
	public bool Velocities = false;
	public bool Trajectory = false;
	public bool InspectSettings = false;

	private bool AutoFocus = false;
	private float FocusHeight = 1f;
	private float FocusOffset = 0f;
	private float FocusDistance = 2.5f;
	private float FocusAngle = 0f;
	private float FocusSmoothing = 0.05f;

	private bool Playing = false;
	private float Timescale = 1f;
	private float Timestamp = 0f;
	private float Window = 1f;

	private File Instance = null;
	private Actor Actor = null;
	private Transform Environment = null;

	public float GetWindow() {
		return GetCurrentFile() == null ? 0f : Window * GetCurrentFile().Data.GetTotalTime();
	}

	public void SetMirror(bool value) {
		if(Mirror != value) {
			Mirror = value;
			LoadFrame(Timestamp);
		}
	}

	public void SetScaling(float value) {
		if(GetCurrentFile().Data.Scaling != value) {
			GetCurrentFile().Data.Scaling = value;
			LoadFrame(Timestamp);
		}
	}

	public void SetAutoFocus(bool value) {
		if(AutoFocus != value) {
			AutoFocus = value;
			if(!AutoFocus) {
				Vector3 position =  SceneView.lastActiveSceneView.camera.transform.position;
				Quaternion rotation = Quaternion.Euler(0f, SceneView.lastActiveSceneView.camera.transform.rotation.eulerAngles.y, 0f);
				SceneView.lastActiveSceneView.LookAtDirect(position, rotation, 0f);
			}
		}
	}

	public Actor GetActor() {
		if(Actor == null) {
			Actor = transform.GetComponentInChildren<Actor>();
		}
		if(Actor == null) {
			Actor = CreateSkeleton();
		}
		return Actor;
	}

	public File[] GetFiles() {
		return Files;
	}

	public Transform GetEnvironment() {
		if(Environment == null) {
			Environment = transform.Find("Environment");
		}
		if(Environment == null) {
			Environment = new GameObject("Environment").transform;
			Environment.SetParent(transform);
		}
		return Environment;
	}

	public File GetCurrentFile() {
		if(Instance != null) {
			if(Instance.Data == null || Instance.Environment == null) {
				Instance = null;
			}
		}
		return Instance;
	}

	public Frame GetCurrentFrame() {
		return GetCurrentFile() == null ? null : GetCurrentFile().Data.GetFrame(Timestamp);
	}

	public void Import() {
		string[] assets = AssetDatabase.FindAssets("t:MotionData", new string[1]{Folder});
		List<File> files = new List<File>();
		for(int i=0; i<assets.Length; i++) {
			File file = new File();
			file.Index = i;
			file.Data = (MotionData)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(assets[i]), typeof(MotionData));
			files.Add(file);
		}
		Files = files.ToArray();
		for(int i=0; i<GetEnvironment().childCount; i++) {
			if(!System.Array.Exists(Files, x => x.Data.name == GetEnvironment().GetChild(i).name)) {
				Utility.Destroy(GetEnvironment().GetChild(i).gameObject);
				i--;
			}
		}
		for(int i=0; i<Files.Length; i++) {
			Files[i].Environment = GetEnvironment().Find(Files[i].Data.name);
			if(Files[i].Environment == null) {
				Files[i].Environment = new GameObject(Files[i].Data.name).transform;
				Files[i].Environment.SetParent(GetEnvironment());
			}
		}
		//Finalise
		for(int i=0; i<Files.Length; i++) {
			Files[i].Environment.gameObject.SetActive(false);
			Files[i].Environment.SetSiblingIndex(i);
		}
		Initialise();
	}

	public void Initialise() {
		for(int i=0; i<Files.Length; i++) {
			if(Files[i].Data == null) {
				Utility.Destroy(Files[i].Environment.gameObject);
				ArrayExtensions.RemoveAt(ref Files, i);
				i--;
			}
		}
		if(GetCurrentFile() == null && Files.Length > 0) {
			LoadFile(Files[0]);
		}
	}
	
	public void Save(File file) {
		if(file != null) {
			EditorUtility.SetDirty(file.Data);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
	}

	public void LoadFile(File file) {
		if(Instance != file) {
			if(Instance != null) {
				Instance.Environment.gameObject.SetActive(false);
				Save(Instance);
			}
			Instance = file;
			if(Instance != null) {
				Instance.Environment.gameObject.SetActive(true);
				LoadFrame(0f);
			}
		}
	}

	public void LoadFrame(float timestamp) {
		Timestamp = timestamp;

		if(Mirror) {
			GetEnvironment().localScale = Vector3.one.GetMirror(GetCurrentFile().Data.GetAxis(GetCurrentFile().Data.MirrorAxis));
		} else {
			GetEnvironment().localScale = Vector3.one;
		}

		Frame frame = GetCurrentFrame();
		Matrix4x4 root = frame.GetRootTransformation(Mirror);
		GetActor().GetRoot().position = root.GetPosition();
		GetActor().GetRoot().rotation = root.GetRotation();
		Matrix4x4[] posture = frame.GetBoneTransformations(Mirror);
		for(int i=0; i<Mathf.Min(GetActor().Bones.Length, posture.Length); i++) {
			GetActor().Bones[i].Transform.position = posture[i].GetPosition();
			GetActor().Bones[i].Transform.rotation = posture[i].GetRotation();
		}

		/*
		if(AutoFocus) {
			if(SceneView.lastActiveSceneView != null) {
				Vector3 lastPosition = SceneView.lastActiveSceneView.camera.transform.position;
				Quaternion lastRotation = SceneView.lastActiveSceneView.camera.transform.rotation;
				Vector3 position = GetState().Root.GetPosition();
				position.y += FocusHeight;
				Quaternion rotation = GetState().Root.GetRotation();
				rotation.x = 0f;
				rotation.z = 0f;
				rotation = Quaternion.Euler(0f, ShowMirror ? Mathf.Repeat(FocusAngle + 0f, 360f) : FocusAngle, 0f) * rotation;
				position += FocusOffset * (rotation * Vector3.right);
				SceneView.lastActiveSceneView.LookAtDirect(Vector3.Lerp(lastPosition, position, 1f-FocusSmoothing), Quaternion.Slerp(lastRotation, rotation, (1f-FocusSmoothing)), FocusDistance*(1f-FocusSmoothing));
			}
		}
		*/
	}

	public void LoadFrame(int index) {
		LoadFrame(GetCurrentFile().Data.GetFrame(index).Timestamp);
	}

	public void LoadPreviousFrame() {
		LoadFrame(Mathf.Max(GetCurrentFrame().Index - 1, 1));
	}

	public void LoadNextFrame() {
		LoadFrame(Mathf.Min(GetCurrentFrame().Index + 1, GetCurrentFile().Data.GetTotalFrames()));
	}

	public void PlayAnimation() {
		if(Playing) {
			return;
		}
		Playing = true;
		EditorCoroutines.StartCoroutine(Play(), this);
	}

	public void StopAnimation() {
		if(!Playing) {
			return;
		}
		Playing = false;
		EditorCoroutines.StopCoroutine(Play(), this);
	}

	private IEnumerator Play() {
		System.DateTime timestamp = Utility.GetTimestamp();
		while(GetCurrentFile() != null) {
			Timestamp += Timescale * (float)Utility.GetElapsedTime(timestamp);
			if(Timestamp > GetCurrentFile().Data.GetTotalTime()) {
				Timestamp = Mathf.Repeat(Timestamp, GetCurrentFile().Data.GetTotalTime());
			}
			timestamp = Utility.GetTimestamp();
			LoadFrame(Timestamp);
			yield return new WaitForSeconds(0f);
		}
	}

	public Actor CreateSkeleton() {
		if(GetCurrentFile() == null) {
			return null;
		}
		Actor actor = new GameObject("Skeleton").AddComponent<Actor>();
		actor.transform.SetParent(transform);
		string[] names = new string[GetCurrentFile().Data.Source.Bones.Length];
		string[] parents = new string[GetCurrentFile().Data.Source.Bones.Length];
		for(int i=0; i<GetCurrentFile().Data.Source.Bones.Length; i++) {
			names[i] = GetCurrentFile().Data.Source.Bones[i].Name;
			parents[i] = GetCurrentFile().Data.Source.Bones[i].Parent;
		}
		List<Transform> instances = new List<Transform>();
		for(int i=0; i<names.Length; i++) {
			Transform instance = new GameObject(names[i]).transform;
			instance.SetParent(parents[i] == "None" ? actor.GetRoot() : actor.FindTransform(parents[i]));
			instances.Add(instance);
		}
		actor.ExtractSkeleton(instances.ToArray());
		return actor;
	}

	public void CopyHierarchy()  {
		for(int i=0; i<GetCurrentFile().Data.Source.Bones.Length; i++) {
			if(GetActor().FindBone(GetCurrentFile().Data.Source.Bones[i].Name) != null) {
				GetCurrentFile().Data.Source.Bones[i].Active = true;
			} else {
				GetCurrentFile().Data.Source.Bones[i].Active = false;
			}
		}
	}

	public void Draw() {
		if(GetCurrentFile() == null) {
			return;
		}

		//if(ShowMotion) {
		//	for(int i=0; i<GetState().PastBoneTransformations.Count; i++) {
		//		GetActor().DrawSimple(Color.Lerp(UltiDraw.Blue, UltiDraw.Cyan, 1f - (float)(i+1)/6f).Transparent(0.75f), GetState().PastBoneTransformations[i]);
		//	}
		//	for(int i=0; i<GetState().FutureBoneTransformations.Count; i++) {
		//		GetActor().DrawSimple(Color.Lerp(UltiDraw.Red, UltiDraw.Orange, (float)i/5f).Transparent(0.75f), GetState().FutureBoneTransformations[i]);
		//	}
		//}
	
		if(Velocities) {
			UltiDraw.Begin();
			Vector3[] velocities = GetCurrentFrame().GetBoneVelocities(Mirror);
			for(int i=0; i<velocities.Length; i++) {
				UltiDraw.DrawArrow(
					GetActor().Bones[i].Transform.position,
					GetActor().Bones[i].Transform.position + velocities[i],
					0.75f,
					0.0075f,
					0.05f,
					UltiDraw.Purple.Transparent(0.5f)
				);
			}
			UltiDraw.End();
		}

		if(Trajectory) {
			GetCurrentFrame().GetTrajectory(Mirror).Draw();
		}

		for(int i=0; i<GetCurrentFile().Data.Modules.Length; i++) {
			GetCurrentFile().Data.Modules[i].Draw(this);
		}
	}

	void OnRenderObject() {
		Draw();
	}

	void OnDrawGizmos() {
		if(!Application.isPlaying) {
			OnRenderObject();
		}
	}

	[System.Serializable]
	public class File {
		public int Index;
		public MotionData Data;
		public Transform Environment;
	}

	[CustomEditor(typeof(MotionEditor))]
	public class MotionEditor_Editor : Editor {

		public MotionEditor Target;

		private float RefreshRate = 30f;
		private System.DateTime Timestamp;

		public int Index = 0;
		public File[] Instances = new File[0];
		public string[] Names = new string[0];
		public string NameFilter = "";
		public bool ExportFilter = false;
		public bool ExcludeFilter = false;

		void Awake() {
			Target = (MotionEditor)target;
			Target.Initialise();
			Filter();
			Timestamp = Utility.GetTimestamp();
			EditorApplication.update += EditorUpdate;
		}

		void OnDestroy() {
			if(!Application.isPlaying && Target != null) {
				Target.Save(Target.GetCurrentFile());
			}
			EditorApplication.update -= EditorUpdate;
		}

		public void EditorUpdate() {
			if(Utility.GetElapsedTime(Timestamp) >= 1f/RefreshRate) {
				Repaint();
				Timestamp = Utility.GetTimestamp();
			}
		}

		public override void OnInspectorGUI() {
			Undo.RecordObject(Target, Target.name);
			Inspector();
			if(GUI.changed) {
				EditorUtility.SetDirty(Target);
			}
		}
		
		private void Filter() {
			List<File> instances = new List<File>();
			if(NameFilter == string.Empty) {
				instances.AddRange(Target.Files);
			} else {
				for(int i=0; i<Target.Files.Length; i++) {
					if(Target.Files[i].Data.name.ToLowerInvariant().Contains(NameFilter.ToLowerInvariant())) {
						instances.Add(Target.Files[i]);
					}
				}
			}
			if(ExportFilter) {
				for(int i=0; i<instances.Count; i++) {
					if(!instances[i].Data.Export) {
						instances.RemoveAt(i);
						i--;
					}
				}
			}
			if(ExcludeFilter) {
				for(int i=0; i<instances.Count; i++) {
					if(instances[i].Data.Export) {
						instances.RemoveAt(i);
						i--;
					}
				}
			}
			Instances = instances.ToArray();
			Names = new string[Instances.Length];
			for(int i=0; i<Instances.Length; i++) {
				Names[i] = Instances[i].Data.name;
			}
			LoadFile(GetIndex());
		}

		public void SetNameFilter(string filter) {
			if(NameFilter != filter) {
				NameFilter = filter;
				Filter();
			}
		}

		public void SetExportFilter(bool value) {
			if(ExportFilter != value) {
				ExportFilter = value;
				Filter();
			}
		}

		public void SetExcludeFilter(bool value) {
			if(ExcludeFilter != value) {
				ExcludeFilter = value;
				Filter();
			}
		}

		public void LoadFile(int index) {
			if(Index != index) {
				Index = index;
				Target.LoadFile(Index >= 0 ? Target.Files[Instances[Index].Index] : null);
			}
		}

		public void Import() {
			Target.Import();
			Filter();
		}

		public int GetIndex() {
			if(Target.GetCurrentFile() == null) {
				return -1;
			}
			if(Instances.Length == Target.Files.Length) {
				return Target.GetCurrentFile().Index;
			} else {
				return System.Array.FindIndex(Instances, x => x == Target.GetCurrentFile());
			}
		}

		public void Inspector() {
			Index = GetIndex();

			Utility.SetGUIColor(UltiDraw.DarkGrey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Utility.SetGUIColor(UltiDraw.LightGrey);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();

					EditorGUILayout.BeginHorizontal();
					Target.Folder = EditorGUILayout.TextField("Folder", "Assets/" + Target.Folder.Substring(Mathf.Min(7, Target.Folder.Length)));
					if(Utility.GUIButton("Import", UltiDraw.DarkGrey, UltiDraw.White)) {
						Import();
					}
					EditorGUILayout.EndHorizontal();


					Utility.SetGUIColor(Target.GetActor() == null ? UltiDraw.DarkRed : UltiDraw.White);
					Target.Actor = (Actor)EditorGUILayout.ObjectField("Actor", Target.GetActor(), typeof(Actor), true);
					Utility.ResetGUIColor();

					EditorGUILayout.ObjectField("Environment", Target.GetEnvironment(), typeof(Transform), true);

					SetNameFilter(EditorGUILayout.TextField("Name Filter", NameFilter));
					SetExportFilter(EditorGUILayout.Toggle("Export Filter", ExportFilter));
					SetExcludeFilter(EditorGUILayout.Toggle("Exclude Filter", ExcludeFilter));

					if(Instances.Length == 0) {
						LoadFile(-1);
						EditorGUILayout.LabelField("No data available.");
					} else {
						LoadFile(EditorGUILayout.Popup("Data " + "(" + Instances.Length + ")", Index, Names));
						EditorGUILayout.BeginHorizontal();
						LoadFile(EditorGUILayout.IntSlider(Index+1, 1, Instances.Length)-1);
						if(Utility.GUIButton("<", UltiDraw.DarkGrey, UltiDraw.White)) {
							LoadFile(Mathf.Max(Index-1, 0));
						}
						if(Utility.GUIButton(">", UltiDraw.DarkGrey, UltiDraw.White)) {
							LoadFile(Mathf.Min(Index+1, Instances.Length-1));
						}
						EditorGUILayout.EndHorizontal();
					}
				}

				if(Target.GetCurrentFile() != null) {
					Utility.SetGUIColor(UltiDraw.Grey);
					using(new EditorGUILayout.VerticalScope ("Box")) {
						Utility.ResetGUIColor();
						Frame frame = Target.GetCurrentFrame();

						Utility.SetGUIColor(UltiDraw.Mustard);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							EditorGUILayout.BeginHorizontal();
							GUILayout.FlexibleSpace();
							EditorGUILayout.LabelField(Target.GetCurrentFile().Data.name, GUILayout.Width(100f));
							EditorGUILayout.LabelField("Frames: " + Target.GetCurrentFile().Data.GetTotalFrames(), GUILayout.Width(100f));
							EditorGUILayout.LabelField("Time: " + Target.GetCurrentFile().Data.GetTotalTime().ToString("F3") + "s", GUILayout.Width(100f));
							EditorGUILayout.LabelField("Framerate: " + Target.GetCurrentFile().Data.Framerate.ToString("F1") + "Hz", GUILayout.Width(130f));
							EditorGUILayout.LabelField("Timescale:", GUILayout.Width(65f), GUILayout.Height(20f)); 
							Target.Timescale = EditorGUILayout.FloatField(Target.Timescale, GUILayout.Width(30f), GUILayout.Height(20f));
							if(Utility.GUIButton("M", Target.Mirror ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
								Target.SetMirror(!Target.Mirror);
							}
							if(Utility.GUIButton("T", Target.Trajectory ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
								Target.Trajectory = !Target.Trajectory;
							}
							if(Utility.GUIButton("V", Target.Velocities ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
								Target.Velocities = !Target.Velocities;
							}
							GUILayout.FlexibleSpace();
							EditorGUILayout.EndHorizontal();
						}

						Utility.SetGUIColor(UltiDraw.DarkGrey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							EditorGUILayout.BeginHorizontal();
							GUILayout.FlexibleSpace();
							if(Target.Playing) {
								if(Utility.GUIButton("||", Color.red, Color.black, 20f, 20f)) {
									Target.StopAnimation();
								}
							} else {
								if(Utility.GUIButton("|>", Color.green, Color.black, 20f, 20f)) {
									Target.PlayAnimation();
								}
							}
							if(Utility.GUIButton("<<", UltiDraw.Grey, UltiDraw.White, 30f, 20f)) {
								Target.LoadFrame(Mathf.Max(Target.GetCurrentFrame().Index - Mathf.RoundToInt(Target.GetCurrentFile().Data.Framerate), 1));
							}
							if(Utility.GUIButton("<", UltiDraw.Grey, UltiDraw.White, 20f, 20f)) {
								Target.LoadPreviousFrame();
							}
							if(Utility.GUIButton(">", UltiDraw.Grey, UltiDraw.White, 20f, 20f)) {
								Target.LoadNextFrame();
							}
							if(Utility.GUIButton(">>", UltiDraw.Grey, UltiDraw.White, 30f, 20f)) {
								Target.LoadFrame(Mathf.Min(Target.GetCurrentFrame().Index + Mathf.RoundToInt(Target.GetCurrentFile().Data.Framerate), Target.GetCurrentFile().Data.GetTotalFrames()));
							}
							int index = EditorGUILayout.IntSlider(frame.Index, 1, Target.GetCurrentFile().Data.GetTotalFrames(), GUILayout.Width(440f));
							if(index != frame.Index) {
								Target.LoadFrame(index);
							}
							EditorGUILayout.LabelField(frame.Timestamp.ToString("F3") + "s", Utility.GetFontColor(Color.white), GUILayout.Width(50f));
							GUILayout.FlexibleSpace();
							EditorGUILayout.EndHorizontal();
							EditorGUILayout.BeginHorizontal();
							GUILayout.FlexibleSpace();
							Target.Window = EditorGUILayout.Slider(Target.Window, 0f, 1f);
							GUILayout.FlexibleSpace();
							EditorGUILayout.EndHorizontal();
						}
					}
					for(int i=0; i<Target.GetCurrentFile().Data.Modules.Length; i++) {
						Target.GetCurrentFile().Data.Modules[i].Inspector(Target);
					}
					string[] modules = new string[(int)Module.TYPE.Length+1];
					modules[0] = "Add Module...";
					for(int i=1; i<modules.Length; i++) {
						modules[i] = ((Module.TYPE)(i-1)).ToString();
					}
					int module = EditorGUILayout.Popup(0, modules);
					if(module > 0) {
						Target.GetCurrentFile().Data.AddModule((Module.TYPE)(module-1));
					}
					if(Utility.GUIButton("Settings", Target.InspectSettings ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
						Target.InspectSettings = !Target.InspectSettings;
					}
					if(Target.InspectSettings) {
						Utility.SetGUIColor(UltiDraw.LightGrey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							Target.GetCurrentFile().Data.Export = EditorGUILayout.Toggle("Export", Target.GetCurrentFile().Data.Export);
							Target.SetScaling(EditorGUILayout.FloatField("Scaling", Target.GetCurrentFile().Data.Scaling));
							Target.GetCurrentFile().Data.RootSmoothing = EditorGUILayout.IntField("Root Smoothing", Target.GetCurrentFile().Data.RootSmoothing);
							Target.GetCurrentFile().Data.Ground = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUILayout.MaskField("Ground Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Target.GetCurrentFile().Data.Ground), InternalEditorUtility.layers));
							Target.GetCurrentFile().Data.MirrorAxis = (MotionData.AXIS)EditorGUILayout.EnumPopup("Mirror Axis", Target.GetCurrentFile().Data.MirrorAxis);
							string[] names = new string[Target.GetCurrentFile().Data.Source.Bones.Length];
							for(int i=0; i<Target.GetCurrentFile().Data.Source.Bones.Length; i++) {
								names[i] = Target.GetCurrentFile().Data.Source.Bones[i].Name;
							}
							for(int i=0; i<Target.GetCurrentFile().Data.Source.Bones.Length; i++) {
								EditorGUILayout.BeginHorizontal();
								Target.GetCurrentFile().Data.Source.Bones[i].Active = EditorGUILayout.Toggle(Target.GetCurrentFile().Data.Source.Bones[i].Active);
								EditorGUI.BeginDisabledGroup(true);
								EditorGUILayout.TextField(names[i]);
								EditorGUI.EndDisabledGroup();
								Target.GetCurrentFile().Data.SetSymmetry(i, EditorGUILayout.Popup(Target.GetCurrentFile().Data.Symmetry[i], names));
								Target.GetCurrentFile().Data.Source.Bones[i].Alignment = EditorGUILayout.Vector3Field("", Target.GetCurrentFile().Data.Source.Bones[i].Alignment);
								EditorGUILayout.EndHorizontal();
							}

							Utility.SetGUIColor(UltiDraw.Grey);
							using(new EditorGUILayout.VerticalScope ("Box")) {
								Utility.ResetGUIColor();

								Utility.SetGUIColor(UltiDraw.Cyan);
								using(new EditorGUILayout.VerticalScope ("Box")) {
									Utility.ResetGUIColor();
									EditorGUILayout.LabelField("Camera");
								}

								if(Utility.GUIButton("Auto Focus", Target.AutoFocus ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
									Target.SetAutoFocus(!Target.AutoFocus);
								}
								Target.FocusHeight = EditorGUILayout.FloatField("Focus Height", Target.FocusHeight);
								Target.FocusOffset = EditorGUILayout.FloatField("Focus Offset", Target.FocusOffset);
								Target.FocusDistance = EditorGUILayout.FloatField("Focus Distance", Target.FocusDistance);
								Target.FocusAngle = EditorGUILayout.Slider("Focus Angle", Target.FocusAngle, 0f, 360f);
								Target.FocusSmoothing = EditorGUILayout.Slider("Focus Smoothing", Target.FocusSmoothing, 0f, 1f);
							}

							if(Utility.GUIButton("Copy Hierarchy", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.CopyHierarchy();
							}
							if(Utility.GUIButton("Detect Symmetry", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.GetCurrentFile().Data.DetectSymmetry();
							}
							if(Utility.GUIButton("Create Skeleton", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.CreateSkeleton();
							}

							EditorGUILayout.BeginHorizontal();
							if(Utility.GUIButton("Add Export Sequence", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.GetCurrentFile().Data.AddSequence(1, Target.GetCurrentFile().Data.GetTotalFrames());
							}
							if(Utility.GUIButton("Remove Export Sequence", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.GetCurrentFile().Data.RemoveSequence();
							}
							EditorGUILayout.EndHorizontal();
							for(int i=0; i<Target.GetCurrentFile().Data.Sequences.Length; i++) {
								Utility.SetGUIColor(UltiDraw.Grey);
								using(new EditorGUILayout.VerticalScope ("Box")) {
									Utility.ResetGUIColor();
									
									EditorGUILayout.BeginHorizontal();
									GUILayout.FlexibleSpace();
									if(Utility.GUIButton("X", Color.cyan, Color.black, 15f, 15f)) {
										Target.GetCurrentFile().Data.Sequences[i].SetStart(Target.GetCurrentFrame().Index);
									}
									EditorGUILayout.LabelField("Start", GUILayout.Width(50f));
									Target.GetCurrentFile().Data.Sequences[i].SetStart(EditorGUILayout.IntField(Target.GetCurrentFile().Data.Sequences[i].Start, GUILayout.Width(100f)));
									EditorGUILayout.LabelField("End", GUILayout.Width(50f));
									Target.GetCurrentFile().Data.Sequences[i].SetEnd(EditorGUILayout.IntField(Target.GetCurrentFile().Data.Sequences[i].End, GUILayout.Width(100f)));
									if(Utility.GUIButton("X", Color.cyan, Color.black, 15f, 15f)) {
										Target.GetCurrentFile().Data.Sequences[i].SetEnd(Target.GetCurrentFrame().Index);
									}
									GUILayout.FlexibleSpace();
									EditorGUILayout.EndHorizontal();
								}
							}
						}
					}
				}
			}
		}

	}

}
#endif