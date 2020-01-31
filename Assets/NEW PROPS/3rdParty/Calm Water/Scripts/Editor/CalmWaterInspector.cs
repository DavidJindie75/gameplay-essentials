using System;
using UnityEngine;

namespace UnityEditor
{
	public class CalmWaterInspector: ShaderGUI
	{
		//Color
		MaterialProperty shallowColor 	= null;
		MaterialProperty depthColor 	= null;
		MaterialProperty depth 			= null;
		MaterialProperty enableDepthFog	= null;

		MaterialProperty edgeFade 		= null;

		//Spec
		MaterialProperty specColor = null;
		MaterialProperty smoothness = null;

		//Distortion
		MaterialProperty bumpMap 	= null;
		MaterialProperty enableLargeBump = null;
		MaterialProperty largeBump 	= null;
		MaterialProperty bumpStrength = null;
		MaterialProperty bumpLargeStrength = null;


		MaterialProperty worldSpace = null;

		//Animation
		MaterialProperty speeds = null;
		MaterialProperty speedsLarge = null;
		//Distortion
		MaterialProperty distortion = null;
		MaterialProperty distortionQuality = null;
				
		//Reflection
		MaterialProperty reflectionType = null;
		MaterialProperty cubeColor = null;
		MaterialProperty cubeMap = null;

		//MaterialProperty reflectionTex = null;
		MaterialProperty reflection = null;
		MaterialProperty fresnel = null;
				
		//Foam
		MaterialProperty foamToggle = null;
		MaterialProperty foamColor = null;
		MaterialProperty foamTex = null;
		MaterialProperty foamSize = null;

		//Displacement
		MaterialProperty displacementMode = null;
		MaterialProperty amplitude 	= null;
		MaterialProperty frequency 	= null;
		MaterialProperty speed		= null;

		MaterialProperty steepness 			= null;
		MaterialProperty waveSpeed 			= null;
		MaterialProperty waveDirectionXY 	= null;
		MaterialProperty waveDirectionZW 	= null;
		MaterialProperty smoothing 			= null;
		MaterialProperty tess 				= null;
		private bool _hasTess = false;

		MaterialEditor m_MaterialEditor;

		private const string _cVersion = "1.6.7";
		
		public void FindProperties(MaterialProperty[] props)
		{

			//Color
			shallowColor 	= FindProperty ("_Color", props);
			depthColor 		= FindProperty ("_DepthColor", props);
			depth 			= FindProperty ("_Depth", props);
			enableDepthFog	= FindProperty ("_EnableFog",props);
			edgeFade		= FindProperty ("_EdgeFade",props);
			//Spec
			specColor 		= FindProperty ("_SpecColor", props);
			smoothness 		= FindProperty ("_Smoothness", props);

			//Distortion
			bumpMap 		= FindProperty ("_BumpMap", props);
			largeBump 		= FindProperty ("_BumpMapLarge", props);
			enableLargeBump	= FindProperty ("_EnableLargeBump", props);
			bumpStrength 	= FindProperty ("_BumpStrength", props);
			bumpLargeStrength = FindProperty ("_BumpLargeStrength", props);

			//Animation
			worldSpace		= FindProperty ("_WorldSpace",props);
			speeds 			= FindProperty ("_Speeds", props);
			speedsLarge 	= FindProperty ("_SpeedsLarge", props);
			
			//Distortion
			distortion 			= FindProperty ("_Distortion", props);
			distortionQuality 	= FindProperty ("_DistortionQuality", props);
			
			//Reflection
			reflectionType 	= FindProperty ("_ReflectionType", props);
			cubeColor 		= FindProperty ("_CubeColor", props);
			cubeMap 		= FindProperty ("_Cube", props);
			reflection 		= FindProperty ("_Reflection", props);
			fresnel 		= FindProperty ("_RimPower", props);
			
			//Foam
			foamToggle 		= FindProperty ("_FOAM", props);
			foamColor 		= FindProperty ("_FoamColor", props);
			foamTex			= FindProperty ("_FoamTex", props);
			foamSize 		= FindProperty ("_FoamSize", props);

			//Displacement
			displacementMode 	= FindProperty ("_DisplacementMode", props);
			amplitude 			= FindProperty ("_Amplitude", props);
			frequency 			= FindProperty ("_Frequency", props);
			speed				= FindProperty ("_Speed",props);
			waveSpeed 			= FindProperty ("_WSpeed", props);
			steepness 			= FindProperty ("_Steepness", props);
			waveDirectionXY 	= FindProperty ("_WDirectionAB", props);
			waveDirectionZW 	= FindProperty ("_WDirectionCD", props);

			smoothing 			= FindProperty ("_Smoothing", props);
			if(_hasTess){
				tess 				= FindProperty ("_Tess", props);
			}

		}
		
		public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
		{
			m_MaterialEditor = materialEditor;
			Material material = materialEditor.target as Material;

			if(material.HasProperty("_Tess")){
				_hasTess = true;
			}else{
				_hasTess = false;
			}

			FindProperties(props); // MaterialProperties can be animated so we do not cache them but fetch them every event to ensure animated values are updated correctly
			ShaderPropertiesGUI(material);
		}
		
		public void ShaderPropertiesGUI(Material material)
		{
			// Use default labelWidth
			EditorGUIUtility.labelWidth = 0f;
			EditorGUIUtility.fieldWidth = 64f;

			Texture2D tex = Resources.Load("CalmWaterLogo") as Texture2D;
			
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label(tex);	
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();



			// Detect any changes to the material
			EditorGUI.BeginChangeCheck();
			{
				// Color
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Color", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(shallowColor,"Shallow Color");
				m_MaterialEditor.ShaderProperty(depthColor,"Depth Color");
				m_MaterialEditor.ShaderProperty(depth,"Depth");
				m_MaterialEditor.ShaderProperty(enableDepthFog,"Enable Depth Fog");
				m_MaterialEditor.ShaderProperty(edgeFade,"Edge Fade");
				GUILayout.EndVertical();

				// Spec
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Specular", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(specColor,"Specular Color");
				m_MaterialEditor.ShaderProperty(smoothness,"Smoothness");
				GUILayout.EndVertical();

				// NormalMap
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Bump", EditorStyles.boldLabel);

				m_MaterialEditor.ShaderProperty(enableLargeBump,"Enable Large Bump");
				m_MaterialEditor.ShaderProperty(bumpMap,"Micro Bump");
				m_MaterialEditor.ShaderProperty(bumpStrength,"Bump Strength");

				if (enableLargeBump.floatValue == 1) 
				{
					m_MaterialEditor.ShaderProperty(largeBump,"Large Bump");
					m_MaterialEditor.ShaderProperty (bumpLargeStrength, "Bump Strength");
				}
					

				GUILayout.Label ("Scroll Animation", EditorStyles.boldLabel);

				// Animation speeds
				Vector2 speeds1 =  EditorGUILayout.Vector2Field ("Micro Speed 1", new Vector2 (speeds.vectorValue.x, speeds.vectorValue.y));
				Vector2 speeds2 =  EditorGUILayout.Vector2Field ("Micro Speed 2", new Vector2 (speeds.vectorValue.z, speeds.vectorValue.w));

				speeds.vectorValue = new Vector4 (speeds1.x,speeds1.y,speeds2.x,speeds2.y);

				if (enableLargeBump.floatValue == 1) 
				{	
					GUILayout.BeginHorizontal ();
					Vector4 LargeSpeed = speedsLarge.vectorValue;

					Vector2 GUILargeSpeed = EditorGUILayout.Vector2Field ("Large Speed", new Vector2(LargeSpeed.x,LargeSpeed.y));
	
					speedsLarge.vectorValue = new Vector4(GUILargeSpeed.x,GUILargeSpeed.y,LargeSpeed.z,LargeSpeed.w);

					GUILayout.EndHorizontal ();
				}

				//Distortion
				GUILayout.Label ("Distortion", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(distortion,"Distortion");
				m_MaterialEditor.ShaderProperty(distortionQuality,"Distortion Quality");
				GUILayout.EndVertical();

				// Reflections
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Reflections", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(reflectionType,"Reflection Type");

				switch((int)reflectionType.floatValue){
					// Mixed Mode Reflections
					case 1 :
						EditorGUILayout.HelpBox("You need to add MirrorReflection script to your object.",MessageType.Info);
						m_MaterialEditor.ShaderProperty(cubeColor,"Cube Color");
						m_MaterialEditor.ShaderProperty(cubeMap,"Cube Map");
						m_MaterialEditor.ShaderProperty(reflection,"Reflection");
						m_MaterialEditor.ShaderProperty(fresnel,"Fresnel");
					break;
					// RealTime Mode Reflections
					case 2 :
						EditorGUILayout.HelpBox("You need to add MirrorReflection script to your object.",MessageType.Info);
						m_MaterialEditor.ShaderProperty(reflection,"Reflection");
						m_MaterialEditor.ShaderProperty(fresnel,"Fresnel");
					break;
					// CubeMap Reflections
					case 3:
						m_MaterialEditor.ShaderProperty(cubeColor,"Cube Color");
						m_MaterialEditor.ShaderProperty(cubeMap,"Cube Map");
						m_MaterialEditor.ShaderProperty(reflection,"Reflection");
						m_MaterialEditor.ShaderProperty(fresnel,"Fresnel");
					break;
					case 4:
					break;
				}
					
				GUILayout.EndVertical();

				// Foam
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Foam", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(foamToggle,"Enable Foam");

				if(foamToggle.floatValue == 1)
				{
					m_MaterialEditor.ShaderProperty(foamColor, "Foam Color");
					m_MaterialEditor.ShaderProperty(foamTex, "Foam Texture");
					m_MaterialEditor.ShaderProperty(foamSize,"Foam Size");
				}

				GUILayout.EndVertical();

				// Displacement
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Displacement", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(displacementMode,"Mode");

				if(displacementMode.floatValue != 0f ){

					EditorGUILayout.HelpBox("You need enough subdivisions in your Geometry.",MessageType.Info);
					EditorGUILayout.HelpBox("To get correct displaced normals, your model needs to be scaled [1,1,1].",MessageType.Info);
				}

				if(displacementMode.floatValue == 1f ){
					m_MaterialEditor.ShaderProperty(amplitude,"Amplitude");
					m_MaterialEditor.ShaderProperty(frequency,"Frequency");
					m_MaterialEditor.ShaderProperty(speed,"Waves Speed");
					m_MaterialEditor.ShaderProperty(smoothing,"Smoothing");
				}

				if(displacementMode.floatValue == 2f ){
				
					m_MaterialEditor.ShaderProperty(amplitude,"Amplitude");
					m_MaterialEditor.ShaderProperty(frequency,"Frequency");
					m_MaterialEditor.ShaderProperty(steepness,"Steepness");
					m_MaterialEditor.ShaderProperty(waveSpeed,"Waves Speed");
					m_MaterialEditor.ShaderProperty(waveDirectionXY,"Waves Directions 1");
					m_MaterialEditor.ShaderProperty(waveDirectionZW,"Waves Directions 2");

					m_MaterialEditor.ShaderProperty(smoothing,"Smoothing");
				}
					

//				if(displacementMode.floatValue == 1)
//				{
//					EditorGUILayout.HelpBox("You need enough subdivisions in your Geometry.",MessageType.Info);
//					if(_hasTess){
//					EditorGUILayout.HelpBox("To get correct displaced normals, your model needs to be scaled [1,1,1].",MessageType.Info);
//						m_MaterialEditor.ShaderProperty(amplitude,"Amplitude");
//						m_MaterialEditor.ShaderProperty(frequency,"Frequency");
//						m_MaterialEditor.ShaderProperty(steepness,"Steepness");
//						m_MaterialEditor.ShaderProperty(waveSpeed,"Waves Speed");
//						m_MaterialEditor.ShaderProperty(waveDirectionXY,"Waves Directions 1");
//						m_MaterialEditor.ShaderProperty(waveDirectionZW,"Waves Directions 2");
//
//						m_MaterialEditor.ShaderProperty(smoothing,"Smoothing");
//					}else{
//						m_MaterialEditor.ShaderProperty(amplitude,"Amplitude");
//						m_MaterialEditor.ShaderProperty(frequency,"Frequency");
//						m_MaterialEditor.ShaderProperty(waveSpeed,"Waves Speed");
//					}
//				}
				GUILayout.EndVertical();


				if(_hasTess){
					GUILayout.BeginVertical("", GUI.skin.box);
					GUILayout.Label ("Tessellation", EditorStyles.boldLabel);
					m_MaterialEditor.ShaderProperty(tess,"Tessellation Level");
					GUILayout.EndVertical();
				}

				// Options
				GUILayout.BeginVertical("", GUI.skin.box);
				GUILayout.Label ("Options", EditorStyles.boldLabel);
				m_MaterialEditor.ShaderProperty(worldSpace, "WorldSpace UV");
				GUILayout.EndVertical ();

				// Version
				GUIStyle boldRight = new GUIStyle ();
				boldRight.alignment = TextAnchor.MiddleRight;
				boldRight.fontStyle = FontStyle.Bold;

				GUILayout.Label ("Version " + _cVersion,boldRight);
				GUILayout.Space (3f);
			}
			
		}
		

	}
}