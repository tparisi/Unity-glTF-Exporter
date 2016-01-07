/***************************************************************************
GlamExport
 - Unity3D Scriptable Wizard to export Hierarchy or Project objects as glTF


****************************************************************************/

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Reflection;


public class SceneToGlTFWiz : ScriptableWizard
{
//	static public List<GlTF_Accessor> Accessors;
//	static public List<GlTF_BufferView> BufferViews;
	static public GlTF_Writer writer;

    static public string path = "?";
	static XmlDocument xdoc;
	static string savedPath = EditorPrefs.GetString ("GlTFPath", "/");
	static string savedFile = EditorPrefs.GetString ("GlTFFile", "test.gltf");

	[MenuItem ("File/Export/glTF")]
	static void CreateWizard()
	{
		savedPath = EditorPrefs.GetString ("glTFPath", "/");
		savedFile = EditorPrefs.GetString ("glTFFile", "test.gltf");
		Debug.Log ("remembered "+savedPath+"   "+savedFile);
		path = savedPath + "/"+ savedFile;
		ScriptableWizard.DisplayWizard("Export Selected Stuff to glTF", typeof(SceneToGlTFWiz), "Export");
	}

	void OnWizardUpdate ()
	{
//		Texture[] txs = Selection.GetFiltered(Texture, SelectionMode.Assets);
//		Debug.Log("found "+txs.Length);
	}

    void OnWizardCreate() // Create (Export) button has been hit (NOT wizard has been created!)
    {
		writer = new GlTF_Writer();
		writer.Init ();
/*
		Object[] deps = EditorUtility.CollectDependencies  (trs);
		foreach (Object o in deps)
		{
			Debug.Log("obj "+o.name+"  "+o.GetType());
		}
*/		
		
		path = EditorUtility.SaveFilePanel("Save glTF file as", savedPath, savedFile, "gltf");
		if (path.Length != 0)
		{
			Debug.Log ("attempting to save to "+path);
			writer.OpenFiles (path);

			// FOR NOW!
			GlTF_Sampler sampler = new GlTF_Sampler("sampler1"); // make the default one for now
			GlTF_Writer.samplers.Add (sampler);
			// first, collect objects in the scene, add to lists
			Transform[] trs = Selection.GetTransforms (SelectionMode.Deep);
			foreach (Transform tr in trs)
			{
				if (tr.camera != null)
				{
					if (tr.camera.isOrthoGraphic)
					{
						GlTF_Orthographic cam;
						cam = new GlTF_Orthographic();
						cam.type = "orthographic";
						cam.zfar = tr.camera.farClipPlane;
						cam.znear = tr.camera.nearClipPlane;
						cam.name = tr.name;
						//cam.orthographic.xmag = tr.camera.
						GlTF_Writer.cameras.Add(cam);
					}
					else
					{
						GlTF_Perspective cam;
						cam = new GlTF_Perspective();
						cam.type = "perspective";
						cam.zfar = tr.camera.farClipPlane;
						cam.znear = tr.camera.nearClipPlane;
						cam.aspect_ratio = tr.camera.aspect;
						cam.yfov = tr.camera.fieldOfView;
						cam.name = tr.name;
						GlTF_Writer.cameras.Add(cam);
					}
				}
				
				if (tr.light != null)
				{
					switch (tr.light.type)
					{
					case LightType.Point:
						GlTF_PointLight pl = new GlTF_PointLight();
						pl.color = new GlTF_ColorRGB (tr.light.color);
						pl.name = tr.name;
						GlTF_Writer.lights.Add (pl);
						break;

					case LightType.Spot:
						GlTF_SpotLight sl = new GlTF_SpotLight();
						sl.color = new GlTF_ColorRGB (tr.light.color);
						sl.name = tr.name;
						GlTF_Writer.lights.Add (sl);
						break;
						
					case LightType.Directional:
						GlTF_DirectionalLight dl = new GlTF_DirectionalLight();
						dl.color = new GlTF_ColorRGB (tr.light.color);
						dl.name = tr.name;
						GlTF_Writer.lights.Add (dl);
						break;
						
					case LightType.Area:
						GlTF_AmbientLight al = new GlTF_AmbientLight();
						al.color = new GlTF_ColorRGB (tr.light.color);
						al.name = tr.name;
						GlTF_Writer.lights.Add (al);
						break;
					}
				}

				MeshRenderer mr = tr.GetComponent<MeshRenderer>();
				if (mr != null)
				{
					MeshFilter mf = tr.GetComponent<MeshFilter>();
					Mesh m = mf.sharedMesh;
					GlTF_Accessor normalAccessor = new GlTF_Accessor("normalAccessor-" + tr.name + "_FIXTHIS", "VEC3", "FLOAT");
					GlTF_Accessor positionAccessor = new GlTF_Accessor("positionAccessor-" + tr.name + "_FIXTHIS", "VEC3", "FLOAT");
					GlTF_Accessor texCoord0Accessor = new GlTF_Accessor("texCoord0Accessor-" + tr.name + "_FIXTHIS", "VEC2", "FLOAT");
					GlTF_Accessor indexAccessor = new GlTF_Accessor("indicesAccessor-" + tr.name + "_FIXTHIS", "SCALAR", "USHORT");
					indexAccessor.bufferView = GlTF_Writer.ushortBufferView;
					normalAccessor.bufferView = GlTF_Writer.vec3BufferView;
					positionAccessor.bufferView = GlTF_Writer.vec3BufferView;
					texCoord0Accessor.bufferView = GlTF_Writer.vec2BufferView;
					GlTF_Mesh mesh = new GlTF_Mesh();
					mesh.name = "mesh-" + tr.name;
					GlTF_Primitive primitive = new GlTF_Primitive();
					primitive.name = "primitive-"+tr.name+"_FIXTHIS";
					GlTF_Attributes attributes = new GlTF_Attributes();
					attributes.normalAccessor = normalAccessor;
					attributes.positionAccessor = positionAccessor;
					attributes.texCoord0Accessor = texCoord0Accessor;
					primitive.attributes = attributes;
					primitive.indices = indexAccessor;
					mesh.primitives.Add (primitive);
					mesh.Populate (m);
					GlTF_Writer.accessors.Add (normalAccessor);
					GlTF_Writer.accessors.Add (positionAccessor);
					GlTF_Writer.accessors.Add (texCoord0Accessor);
					GlTF_Writer.accessors.Add (indexAccessor);
					GlTF_Writer.meshes.Add (mesh);

					// next, add material(s) to dictionary (when unique)
					string matName = mr.sharedMaterial.name;
					if (matName == "")
						matName = "material-diffault-diffuse";
					else
						matName = "material-" + matName;
					primitive.materialName = matName;
					if (!GlTF_Writer.materials.ContainsKey (matName))
					{
						GlTF_Material material = new GlTF_Material();
						material.name = matName;
						if (mr.sharedMaterial.HasProperty ("shininess"))
							material.shininess = mr.sharedMaterial.GetFloat("shininess");
						material.diffuse = new GlTF_MaterialColor ("diffuse", mr.sharedMaterial.color);
						//material.ambient = new GlTF_Color ("ambient", mr.material.color);
						
						if (mr.sharedMaterial.HasProperty ("specular"))
						{
							Color sc = mr.sharedMaterial.GetColor ("specular");
							material.specular = new GlTF_MaterialColor ("specular", sc);
						}
						GlTF_Writer.materials.Add (material.name, material);

						// if there are textures, add them too
						if (mr.sharedMaterial.mainTexture != null)
						{
							if (!GlTF_Writer.textures.ContainsKey (mr.sharedMaterial.mainTexture.name))
							{
								GlTF_Texture texture = new GlTF_Texture ();
								texture.name = mr.sharedMaterial.mainTexture.name;
								texture.source = AssetDatabase.GetAssetPath(mr.sharedMaterial.mainTexture);
								texture.samplerName = sampler.name; // FIX! For now!
								GlTF_Writer.textures.Add (mr.sharedMaterial.mainTexture.name, texture);
								material.diffuse = new GlTF_MaterialTexture ("diffuse", texture);
							}
						}
					}
					
				}

				Animation a = tr.animation;
				
//				Animator a = tr.GetComponent<Animator>();				
				if (a != null)
				{
					AnimationClip[] clips = AnimationUtility.GetAnimationClips(tr.gameObject);
					int nClips = clips.Length;
//					int nClips = a.GetClipCount();
					for (int i = 0; i < nClips; i++)
					{
						GlTF_Animation anim = new GlTF_Animation(a.name);
						anim.Populate (clips[i]);
						GlTF_Writer.animations.Add (anim);
					}
				}

	
				// next, build hierarchy of nodes
				GlTF_Node node = new GlTF_Node();
				if (tr.parent != null)
					node.hasParent = true;
				if (tr.localPosition != Vector3.zero)
					node.translation = new GlTF_Translation (tr.localPosition);
				if (tr.localScale != Vector3.one)
					node.scale = new GlTF_Scale (tr.localScale);
				if (tr.localRotation != Quaternion.identity)
					node.rotation = new GlTF_Rotation (tr.localRotation);
				node.name = tr.name;
				if (tr.camera != null)
				{
					node.cameraName = tr.name;
				}
				else if (tr.light != null)
					node.lightName = tr.name;
				else if (mr != null)
				{
					node.meshNames.Add ("mesh-" + tr.name);
				}

				foreach (Transform t in tr.transform)
					node.childrenNames.Add ("node-" + t.name);
				
				GlTF_Writer.nodes.Add (node);
			}

			// third, add meshes etc to byte stream, keeping track of buffer offsets
			writer.Write ();
			writer.CloseFiles();
		}
	}
	
	static string toGlTFname(string name)
	{
		// remove spaces and illegal chars, replace with underscores
		string correctString = name.Replace(" ", "_");
		// make sure it doesn't start with a number
		return correctString; 
	}
	
	static bool isInheritedFrom (Type t, Type baseT)
	{
		if (t == baseT)
			return true;
		t = t.BaseType;
		while (t != null && t != typeof(System.Object))
		{
			if (t == baseT)
				return true;
			t = t.BaseType;
		}
		return false;
	}
}

public class GlTF_Writer {
	public static StreamWriter jsonWriter;
	public static Stream binFile;
	public static int indent = 0;
	public static string binFileName;
	static bool[] firsts = new bool[100];
	public static GlTF_BufferView ushortBufferView = new GlTF_BufferView("ushortBufferView");
	public static GlTF_BufferView floatBufferView = new GlTF_BufferView("floatBufferView");
	public static GlTF_BufferView vec2BufferView = new GlTF_BufferView("vec2BufferView");
	public static GlTF_BufferView vec3BufferView = new GlTF_BufferView("vec3BufferView");
	public static GlTF_BufferView vec4BufferView = new GlTF_BufferView("vec4BufferView");
	public static List<GlTF_BufferView> bufferViews = new List<GlTF_BufferView>();	
	public static List<GlTF_Camera> cameras = new List<GlTF_Camera>();
	public static List<GlTF_Light> lights = new List<GlTF_Light>();
	public static List<GlTF_Mesh> meshes = new List<GlTF_Mesh>();
	public static List<GlTF_Accessor> accessors = new List<GlTF_Accessor>();
	public static List<GlTF_Node> nodes = new List<GlTF_Node>();
	public static Dictionary<string, GlTF_Material> materials = new Dictionary<string, GlTF_Material>();
	public static Dictionary<string, GlTF_Texture> textures = new Dictionary<string, GlTF_Texture>();
	public static List<GlTF_Sampler> samplers = new List<GlTF_Sampler>();
	public static List<GlTF_Animation> animations = new List<GlTF_Animation>();
	// GlTF_Technique
	
	public void Init()
	{
		firsts = new bool[100];
		ushortBufferView = new GlTF_BufferView("ushortBufferView");
		floatBufferView = new GlTF_BufferView("floatBufferView");
		vec2BufferView = new GlTF_BufferView("vec2BufferView");
		vec3BufferView = new GlTF_BufferView("vec3BufferView");
		vec4BufferView = new GlTF_BufferView("vec4BufferView");
		bufferViews = new List<GlTF_BufferView>();	
		cameras = new List<GlTF_Camera>();
		lights = new List<GlTF_Light>();
		meshes = new List<GlTF_Mesh>();
		accessors = new List<GlTF_Accessor>();
		nodes = new List<GlTF_Node>();
		materials = new Dictionary<string, GlTF_Material>();
		textures = new Dictionary<string, GlTF_Texture>();
		samplers = new List<GlTF_Sampler>();
		animations = new List<GlTF_Animation>();
		// GlTF_Technique
	}

	public void Indent() {
		for (int i = 0; i < indent; i++)
			jsonWriter.Write ("\t");
	}
	
	public void IndentIn() {
		indent++;
		firsts[indent] = true;
	}
	
	public void IndentOut() {
		indent--;
	}
	
	public void CommaStart() {
		firsts[indent] = false;
	}
	
	public void CommaNL() {
		if (!firsts[indent])
			jsonWriter.Write (",\n");
//		else
//			jsonWriter.Write ("\n");
		firsts[indent] = false;
	}

	public string name; // name of this object

	public void OpenFiles (string filepath) {
		jsonWriter = new StreamWriter (filepath);
		binFileName = Path.GetFileNameWithoutExtension (filepath) + ".bin";
		binFile = File.Open(binFileName, FileMode.Create);
		//		binWriter = new BinaryWriter (File.Open(binFileName, FileMode.Create));
//		binWriter = new BinaryWriter (File.Open(binFileName, FileMode.Create));
	}
	
	public void CloseFiles() {

		binFile.Close();
		jsonWriter.Close ();
	}
	
	public virtual void Write () {
	
		bufferViews.Add (ushortBufferView);
		bufferViews.Add (floatBufferView);
		bufferViews.Add (vec2BufferView);
		bufferViews.Add (vec3BufferView);
		bufferViews.Add (vec4BufferView);
		
		// write memory streams to binary file
		ushortBufferView.byteOffset = 0;
		ushortBufferView.memoryStream.WriteTo(binFile);
		floatBufferView.byteOffset = ushortBufferView.byteLength;
		floatBufferView.memoryStream.WriteTo(binFile);
		vec2BufferView.byteOffset = floatBufferView.byteOffset + floatBufferView.byteLength;
		vec2BufferView.memoryStream.WriteTo (binFile);
		vec3BufferView.byteOffset = vec2BufferView.byteOffset + vec2BufferView.byteLength;
		vec3BufferView.memoryStream.WriteTo (binFile);
		vec4BufferView.byteOffset = vec3BufferView.byteOffset + vec3BufferView.byteLength;
		vec4BufferView.memoryStream.WriteTo (binFile);

		jsonWriter.Write ("{\n");
		IndentIn();
		
// FIX: Should support multiple buffers
		CommaNL();
		Indent();	jsonWriter.Write ("\"buffers\": {\n");
		IndentIn();
		Indent();	jsonWriter.Write ("\"" + Path.GetFileNameWithoutExtension(GlTF_Writer.binFileName) +"\": {\n");
		IndentIn();
		Indent();	jsonWriter.Write ("\"byteLength\": "+ (vec4BufferView.byteOffset+vec4BufferView.byteLength)+",\n");
		Indent();	jsonWriter.Write ("\"type\": \"arraybuffer\",\n");
		Indent();	jsonWriter.Write ("\"uri\": \"" + GlTF_Writer.binFileName + "\"\n");

		IndentOut();
		Indent();	jsonWriter.Write ("}\n");
		
		IndentOut();
		Indent();	jsonWriter.Write ("}");
		
		if (cameras != null && cameras.Count > 0)
		{
			CommaNL();
			Indent();		jsonWriter.Write ("\"cameras\": {\n");
			IndentIn();
			foreach (GlTF_Camera c in cameras)
			{
				CommaNL();
				c.Write ();
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();		jsonWriter.Write ("}");
		}

		if (accessors != null && accessors.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"accessors\": {\n");
			IndentIn();
			foreach (GlTF_Accessor a in accessors)
			{
				CommaNL();
				a.Write ();
			}			
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("}");
		}
		
		if (bufferViews != null && bufferViews.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"bufferViews\": {\n");
			IndentIn();
			foreach (GlTF_BufferView bv in bufferViews)
			{
				CommaNL();
				bv.Write ();
			}			
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("}");
		}

		if (meshes != null && meshes.Count > 0)
		{
			CommaNL();
			Indent();
			jsonWriter.Write ("\"meshes\": {\n");
			IndentIn();
			foreach (GlTF_Mesh m in meshes)
			{
				CommaNL();
				m.Write ();
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();
			jsonWriter.Write ("}");
		}

		// if (techniques != null && techniques.Count > 0)
		CommaNL();
		string tqs = @"
	'techniques': {
		'technique1': {
			'parameters': {
				'ambient': {
					'type': 35666
				},
				'diffuse': {
					'type': 35678
				},
				'emission': {
					'type': 35666
				},
				'light0Color': {
					'type': 35665,
					'value': [
					    1,
					    1,
					    1
					    ]
				},
				'light0Transform': {
					'semantic': 'MODELVIEW',
					'source': 'directionalLight1',
					'type': 35676
				},
				'modelViewMatrix': {
					'semantic': 'MODELVIEW',
					'type': 35676
				},
				'normal': {
					'semantic': 'NORMAL',
					'type': 35665
				},
				'normalMatrix': {
					'semantic': 'MODELVIEWINVERSETRANSPOSE',
					'type': 35675
				},
				'position': {
					'semantic': 'POSITION',
					'type': 35665
				},
				'projectionMatrix': {
					'semantic': 'PROJECTION',
					'type': 35676
				},
				'shininess': {
					'type': 5126
				},
				'specular': {
					'type': 35666
				},
				'texcoord0': {
					'semantic': 'TEXCOORD_0',
					'type': 35664
				}
			},
			'pass': 'defaultPass',
			'passes': {
				'defaultPass': {
					'details': {
						'commonProfile': {
							'extras': {
								'doubleSided': false
							},
							'lightingModel': 'Blinn',
							'parameters': [
							    'ambient',
							    'diffuse',
							    'emission',
							    'light0Color',
							    'light0Transform',
							    'modelViewMatrix',
							    'normalMatrix',
							    'projectionMatrix',
							    'shininess',
							    'specular'
							    ],
							'texcoordBindings': {
								'diffuse': 'TEXCOORD_0'
							}
						},
						'type': 'COLLADA-1.4.1/commonProfile'
					},
					'instanceProgram': {
						'attributes': {
							'a_normal': 'normal',
							'a_position': 'position',
							'a_texcoord0': 'texcoord0'
						},
						'program': 'program_0',
						'uniforms': {
							'u_ambient': 'ambient',
							'u_diffuse': 'diffuse',
							'u_emission': 'emission',
							'u_light0Color': 'light0Color',
							'u_light0Transform': 'light0Transform',
							'u_modelViewMatrix': 'modelViewMatrix',
							'u_normalMatrix': 'normalMatrix',
							'u_projectionMatrix': 'projectionMatrix',
							'u_shininess': 'shininess',
							'u_specular': 'specular'
						}
					},
					'states': {
						'enable': [
						    2884,
						    2929
						    ]
					}
				}
			}
		}
	}";
		tqs = tqs.Replace ("'", "\"");
		jsonWriter.Write (tqs);

		if (samplers.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"samplers\": {\n");
			IndentIn();
			foreach (GlTF_Sampler s in samplers)
			{
				CommaNL();
				s.Write ();
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("}");
		}
		
		if (textures.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"textures\": {\n");
			IndentIn();
			foreach (KeyValuePair<string,GlTF_Texture> t in textures)
			{
				CommaNL();
				t.Value.Write ();
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("}");
		}
		
		if (materials.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"materials\": {\n");
			IndentIn();
			foreach (KeyValuePair<string,GlTF_Material> m in materials)
			{
				CommaNL();
				m.Value.Write ();
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("}");
		}
		
		if (animations.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"animations\": {\n");
			IndentIn();
			foreach (GlTF_Animation a in animations)
			{
				CommaNL();
				a.Write ();
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("}");
		}
		
		if (nodes != null && nodes.Count > 0)
		{
			CommaNL();
			/*
		    "nodes": {
        "node-Alien": {
            "children": [],
            "matrix": [
*/
			Indent();			jsonWriter.Write ("\"nodes\": {\n");
			IndentIn();
//			bool first = true;
			foreach (GlTF_Node n in nodes)
			{
				CommaNL();
//				if (!first)
//					jsonWriter.Write (",\n");
				n.Write ();
//				first = false;
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();			jsonWriter.Write ("}");
			
		}
		CommaNL();
		
		Indent();			jsonWriter.Write ("\"scene\": \"defaultScene\",\n");
		Indent();			jsonWriter.Write ("\"scenes\": {\n");
		IndentIn();
		Indent();			jsonWriter.Write ("\"defaultScene\": {\n");
		IndentIn();
		CommaNL();
		Indent();			jsonWriter.Write ("\"nodes\": [\n");
		IndentIn();
		foreach (GlTF_Node n in nodes)
		{
			if (!n.hasParent)
			{
				CommaNL();
				Indent();		jsonWriter.Write ("\"node-" + n.name + "\"");
			}
		}
		jsonWriter.WriteLine();
		IndentOut();
		Indent();			jsonWriter.Write ("]\n");
		IndentOut();
		Indent();			jsonWriter.Write ("}\n");
		IndentOut();
		Indent();			jsonWriter.Write ("}\n");
		IndentOut();
		Indent();			jsonWriter.Write ("}");
	}
}

public class GlTF_Accessor : GlTF_Writer {
	public GlTF_BufferView bufferView;//	"bufferView": "bufferView_30",
	public long byteOffset; //": 0,
	public int byteStride;// ": 12,
	public int componentType; // GL enum vals ": BYTE (5120), UNSIGNED_BYTE (5121), SHORT (5122), UNSIGNED_SHORT (5123), FLOAT (5126)
	public int count;//": 2399,
	public GlTF_Vector3 max = new GlTF_Vector3();//": [
	public GlTF_Vector3 min = new GlTF_Vector3();//": [
	public string aType = "SCALAR"; // ": "VEC3" NOTE: SHOULD BE ENUM, USE ToString to output it

	public GlTF_Accessor (string n) { name = n; }
	public GlTF_Accessor (string n, string t, string c) {
		name = n;
		aType = t;
		switch (t)
		{
			case "SCALAR":
				byteStride = 0;
				break;
			case "VEC2":
				byteStride = 8;
				break;
			case "VEC3":
				byteStride = 12;
				break;
			case "VEC4":
				byteStride = 16;
				break;
		}
		switch (c)
		{
			case "USHORT":
				componentType = 5123;
				break;
			case "FLOAT":
				componentType = 5126;
				break;
		}
	}

	public void Populate (int[] vs, bool flippedTriangle)
	{
		if (aType != "SCALAR")
			throw (new System.Exception());
		byteOffset = bufferView.currentOffset;
		bufferView.Populate (vs, flippedTriangle);
		count = vs.Length;
	}
	
	public void Populate (float[] vs)
	{
		if (aType != "SCALAR")
			throw (new System.Exception());
		byteOffset = bufferView.currentOffset;
		bufferView.Populate (vs);
		count = vs.Length;
	}
	
	public void Populate (Vector2[] v2s)
	{
		if (aType != "VEC2")
			throw (new System.Exception());
		byteOffset = bufferView.currentOffset;	
		Bounds b = new Bounds();
		for (int i = 0; i < v2s.Length; i++)
		{
			bufferView.Populate (v2s[i].x);
			bufferView.Populate (v2s[i].y);
			b.Encapsulate (v2s[i]);
			bufferView.Populate (v2s[i].x);
			bufferView.Populate (v2s[i].y);
		}
		count = v2s.Length;
		min.items[0] = b.min.x;
		min.items[1] = b.min.y;
		max.items[0] = b.max.x;
		max.items[1] = b.max.y;
	}
	
	public void Populate (Vector3[] v3s)
	{
		if (aType != "VEC3")
			throw (new System.Exception());
		Bounds b = new Bounds();
		byteOffset = bufferView.currentOffset;	
		for (int i = 0; i < v3s.Length; i++)
		{
			bufferView.Populate (v3s[i].x);
			bufferView.Populate (v3s[i].y);
			bufferView.Populate (-v3s[i].z);
			b.Encapsulate (v3s[i]);
		}
		count = v3s.Length;
		min.items[0] = b.min.x;
		min.items[1] = b.min.y;
		min.items[2] = b.min.z;
		max.items[0] = b.max.x;
		max.items[1] = b.max.y;
		max.items[2] = b.max.z;
	}
	
	public void Populate (Vector4[] v4s)
	{
		if (aType != "VEC4")
			throw (new System.Exception());
//		Bounds b = new Bounds();
		byteOffset = bufferView.currentOffset;	
		for (int i = 0; i < v4s.Length; i++)
		{
			bufferView.Populate (v4s[i].x);
			bufferView.Populate (v4s[i].y);
			bufferView.Populate (v4s[i].z);
			bufferView.Populate (v4s[i].w);
//			b.Expand (v4s[i]);
		}
		count = v4s.Length;
		/*
		min.items[0] = b.min.x;
		min.items[1] = b.min.y;
		min.items[2] = b.min.z;
		min.items[3] = b.min.w;
		max.items[0] = b.max.x;
		max.items[1] = b.max.y;
		max.items[2] = b.max.z;
		max.items[3] = b.max.w;
		*/
	}
	
	public override void Write ()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"bufferView\": \"" + bufferView.name+"\",\n");
		Indent();		jsonWriter.Write ("\"byteOffset\": " + byteOffset + ",\n");
		Indent();		jsonWriter.Write ("\"byteStride\": " + byteStride + ",\n");
		Indent();		jsonWriter.Write ("\"componentType\": " + componentType + ",\n");
		Indent();		jsonWriter.Write ("\"count\": " + count + ",\n");
		Indent();		jsonWriter.Write ("\"max\": [ ");
		max.WriteVals();
		jsonWriter.Write (" ],\n");
			Indent();		jsonWriter.Write ("\"min\": [ ");
		min.WriteVals();
		jsonWriter.Write (" ],\n");
		Indent();		jsonWriter.Write ("\"type\": \"" + aType + "\"\n");
		IndentOut();
		Indent();	jsonWriter.Write (" }");
	}
}

public class GlTF_BufferView : GlTF_Writer  {
	public string buffer;// ": "duck",
	public long byteLength;//": 25272,
	public long byteOffset;//": 0,
	public int target = 34963;
//	public string target = "ARRAY_BUFFER";
	public int currentOffset = 0;
	public MemoryStream memoryStream = new MemoryStream();
	
	public GlTF_BufferView (string n) { name = n; }
	public GlTF_BufferView (string n, int t) { name = n; target = t; }
	
	public void Populate (int[] vs, bool flippedTriangle)
	{
		if (flippedTriangle)
		{
			for (int i = 0; i < vs.Length; i+=3)
			{
				ushort u = (ushort)vs[i];
				memoryStream.Write (BitConverter.GetBytes(u), 0, 2);
				currentOffset += 2;
				
				u = (ushort)vs[i+2];
				memoryStream.Write (BitConverter.GetBytes(u), 0, 2);
				currentOffset += 2;
				
				u = (ushort)vs[i+1];
				memoryStream.Write (BitConverter.GetBytes(u), 0, 2);
				currentOffset += 2;
			}
		}
		else
		{
			for (int i = 0; i < vs.Length; i++)
			{
				ushort u = (ushort)vs[i];
				memoryStream.Write (BitConverter.GetBytes(u), 0, 2);
				currentOffset += 2;
			}
		}
		byteLength = currentOffset;
	}
	
	public void Populate (float[] vs)
	{
		for (int i = 0; i < vs.Length; i++)
		{
			//			memoryStream.Write (vs[i]);
			//			memoryStream.Write ((byte[])vs, 0, vs.Length * sizeof(int));
			float f = vs[i];
			memoryStream.Write (BitConverter.GetBytes(f), 0, 2);
			currentOffset += 4;
		}
		byteLength = currentOffset;
	}
	
	public void Populate (float v)
	{
		memoryStream.Write (BitConverter.GetBytes(v), 0, 4);
		currentOffset += 4;
		byteLength = currentOffset;
	}
	
	public override void Write ()
	{
		/*
		"bufferView_4642": {
            "buffer": "vc.bin",
            "byteLength": 630080,
            "byteOffset": 0,
            "target": "ARRAY_BUFFER"
        },
	*/
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"buffer\": \"" + Path.GetFileNameWithoutExtension(GlTF_Writer.binFileName)+"\",\n");
		Indent();		jsonWriter.Write ("\"byteLength\": " + byteLength + ",\n");
		Indent();		jsonWriter.Write ("\"byteOffset\": " + byteOffset + ",\n");
		Indent();		jsonWriter.Write ("\"target\": " + target + "\n");
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
}

public class GlTF_Perspective : GlTF_Camera  {
	public float aspect_ratio;
	public float yfov;//": 37.8492,
	public float zfar;//": 100,
	public float znear;//": 0.01
	public GlTF_Perspective() { type = "perspective"; }
	public override void Write ()
	{
	/*
	        "camera_0": {
            "perspective": {
                "yfov": 45,
                "zfar": 3162.76,
                "znear": 12.651
            },
            "type": "perspective"
        }
	*/
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"perspective\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"aspect_ratio\": "+aspect_ratio.ToString()+",\n");
		Indent();		jsonWriter.Write ("\"yfov\": "+yfov.ToString()+",\n");
		Indent();		jsonWriter.Write ("\"zfar\": "+zfar.ToString()+",\n");
		Indent();		jsonWriter.Write ("\"znear\": "+znear.ToString()+"\n");
		IndentOut();
		Indent();		jsonWriter.Write ("},\n");
		Indent();		jsonWriter.Write ("\"type\": \"perspective\"\n");
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
}

public class GlTF_Orthographic : GlTF_Camera {
	public float xmag;
	public float ymag;
	public float zfar;
	public float znear;
	public GlTF_Orthographic() { type = "orthographic"; }
	public override void Write ()
	{
	}
}

public class GlTF_Camera : GlTF_Writer  {
	public string type;// should be enum ": "perspective"
}

public class GlTF_Sampler : GlTF_Writer {
	public int magFilter = 9729;
	public int minFilter = 9729;
	public int wrapS = 10497;
	public int wrapT = 10497;
	
	public GlTF_Sampler (string n) { name = n; }

	public override void Write()
	{
		Indent();	jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();	jsonWriter.Write ("\"magFilter\": " + magFilter + ",\n");
		Indent();	jsonWriter.Write ("\"minFilter\": " + minFilter + ",\n");
		Indent();	jsonWriter.Write ("\"wrapS\": " + wrapS + ",\n");
		Indent();	jsonWriter.Write ("\"wrapT\": " + wrapT + "\n");
		IndentOut();
		Indent();	jsonWriter.Write ("}");		
	}
}

public class GlTF_Texture : GlTF_Writer {
/*
        "texture_O21_jpg": {
            "format": 6408,
            "internalFormat": 6408,
            "sampler": "sampler_0",
            "source": "O21_jpg",
            "target": 3553,
            "type": 5121
        },
*/
	public int format = 6408;
	public int internalFormat = 6408;
	public string samplerName;
	public string source;
	public int target = 3553;
	public int tType = 5121;

	public override void Write()
	{
		Indent();	jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();	jsonWriter.Write ("\"format\": " + format + ",\n");
		Indent();	jsonWriter.Write ("\"internalFormat\": " + internalFormat + ",\n");
		Indent();	jsonWriter.Write ("\"sampler\": \"" + samplerName + "\",\n");
		Indent();	jsonWriter.Write ("\"source\": \"" + source + "\",\n");
		Indent();	jsonWriter.Write ("\"target\": " + target + ",\n");
		Indent();	jsonWriter.Write ("\"type\": " + tType + "\n");
		IndentOut();
		Indent();	jsonWriter.Write ("}");
	}
}


public class GlTF_FloatArray : GlTF_Writer {
	public float[] items;
	public int minItems = 0;
	public int maxItems = 0;

	public GlTF_FloatArray () { }
	public GlTF_FloatArray (string n) { name = n; }
	
	public override void Write()
	{
		if (name.Length > 0)
		{
			Indent();	jsonWriter.Write ("\"" + name + "\": [");
		}
		WriteVals();
		if (name.Length > 0)
		{
			Indent();	jsonWriter.Write ("]");
		}
	}

	public virtual void WriteVals ()
	{
		for (int i = 0; i < maxItems; i++)
		{
			if (i > 0)
				jsonWriter.Write (", ");
			jsonWriter.Write (items[i].ToString ());
		}
	}
}

public class GlTF_Matrix : GlTF_FloatArray {
	public GlTF_Matrix() { minItems = 16; maxItems = 16; items = new float[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f }; }
}
	
public class GlTF_FloatArray4 : GlTF_FloatArray {
	public GlTF_FloatArray4() { minItems = 4; maxItems = 4; items = new float[] { 1.0f, 0.0f, 0.0f, 0.0f }; }
/*
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"rotation\": [ ");
		WriteVals();
		jsonWriter.Write ("]");
	}
*/
}

public class GlTF_Rotation : GlTF_FloatArray4 {
	public GlTF_Rotation(Quaternion q) { name = "rotation"; minItems = 4; maxItems = 4; items = new float[] { q.x, q.y, q.z, q.w }; }
/*
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"rotation\": [ ");
		WriteVals();
		jsonWriter.Write ("]");
	}
*/
}

public class GlTF_Vector3 : GlTF_FloatArray {
	public GlTF_Vector3() { minItems = 3; maxItems = 3; items = new float[] {0f, 0f, 0f}; }
	public GlTF_Vector3(Vector3 v) { minItems = 3; maxItems = 3; items = new float[] {v.x, v.y, v.z}; }
}

public class GlTF_Translation : GlTF_Vector3 {
	public GlTF_Translation (Vector3 v) { items = new float[] {v.x, v.y, v.z }; }
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"translation\": [ ");
		WriteVals();
		jsonWriter.Write ("]");
	}
}

public class GlTF_Scale : GlTF_Vector3 {
	public GlTF_Scale() { items = new float[] {1f, 1f, 1f}; }
	public GlTF_Scale(Vector3 v) { items = new float[] {v.x, v.y, v.z }; }
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"scale\": [ ");
		WriteVals();
		jsonWriter.Write ("]");
	}
}

public class GlTF_Attributes : GlTF_Writer {
	public GlTF_Accessor normalAccessor;
	public GlTF_Accessor positionAccessor;
	public GlTF_Accessor texCoord0Accessor;
	public GlTF_Accessor texCoord1Accessor;

	public void Populate (Mesh m)
	{
		positionAccessor.Populate (m.vertices);
		normalAccessor.Populate (m.normals);
		texCoord0Accessor.Populate (m.uv);
	}

	public override void Write ()
	{
		Indent();	jsonWriter.Write ("\"attributes\": {\n");
		IndentIn();
		if (normalAccessor != null)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"NORMAL\": \"" + normalAccessor.name + "\"");
		}
		if (normalAccessor != null)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"POSITION\": \"" + positionAccessor.name + "\"");
		}
		if (normalAccessor != null)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"TEXCOORD_0\": \"" + texCoord0Accessor.name + "\"");
		}
		//CommaNL();
		jsonWriter.WriteLine();
		IndentOut();
		Indent();	jsonWriter.Write ("}");
	}
	
}

public class GlTF_Primitive : GlTF_Writer {
	public GlTF_Attributes attributes = new GlTF_Attributes();
	public GlTF_Accessor indices;
	public string materialName;
	public int primitive =  4;
	public int semantics = 4;

	public void Populate (Mesh m)
	{
		attributes.Populate (m);
		indices.Populate (m.triangles, true);
	}

	public override void Write ()
	{
		IndentIn();
		CommaNL();
		if (attributes != null)
			attributes.Write();
		CommaNL();
		Indent();	jsonWriter.Write ("\"indices\": \"" + indices.name + "\",\n");
		Indent();	jsonWriter.Write ("\"material\": \"" + materialName + "\",\n");
		Indent();	jsonWriter.Write ("\"primitive\": " + primitive + "\n");
		// semantics
		IndentOut();
	}
}

public class GlTF_Mesh : GlTF_Writer {
	public List<GlTF_Primitive> primitives;
	
	public GlTF_Mesh() { primitives = new List<GlTF_Primitive>(); }
	
	public void Populate (Mesh m)
	{
		primitives[0].Populate (m);
	}

	public override void Write ()
	{
		Indent();	jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();	jsonWriter.Write ("\"name\": \"" + name + "\",\n");
		Indent();	jsonWriter.Write ("\"primitives\": [\n");
		IndentIn();
		foreach (GlTF_Primitive p in primitives)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("{\n");
			p.Write ();
			Indent();	jsonWriter.Write ("}");
		}
		jsonWriter.WriteLine();
		IndentOut();
		Indent();	jsonWriter.Write ("]\n");
		IndentOut();
		Indent();	jsonWriter.Write ("}");
	}
}

public class GlTF_Light : GlTF_Writer {
	public GlTF_ColorRGB color;
	public string type;
//	public override void Write ()
//	{
//	}
}


public class GlTF_ColorRGB : GlTF_Writer {
	Color color;
	public GlTF_ColorRGB (string n) { name = n; }
	public GlTF_ColorRGB (Color c) { color = c; }
	public GlTF_ColorRGB (string n, Color c) { name = n; color = c; }
	public override void Write ()
	{
		Indent();
		if (name.Length > 0)
			jsonWriter.Write ("\"" + name + "\": ");
		else
			jsonWriter.Write ("\"color\": [");
		jsonWriter.Write (color.r.ToString() + ", " + color.g.ToString() + ", " +color.b.ToString()+"]");
	}
}

public class GlTF_ColorRGBA : GlTF_Writer {
	Color color;
	public GlTF_ColorRGBA (string n) { name = n; }
	public GlTF_ColorRGBA (Color c) { color = c; }
	public GlTF_ColorRGBA (string n, Color c) { name = n; color = c; }
	public override void Write ()
	{
		Indent();
		if (name.Length > 0)
			jsonWriter.Write ("\"" + name + "\": [");
		else
			jsonWriter.Write ("\"color\": [");
		jsonWriter.Write (color.r.ToString() + ", " + color.g.ToString() + ", " +color.b.ToString()+ ", " +color.a.ToString()+"]");
	}
}

public class GlTF_AmbientLight : GlTF_Light {
	public override void Write()
	{
		color.Write();
	}
}

public class GlTF_DirectionalLight : GlTF_Light {
	public override void Write()
	{
		color.Write();
	}
}

public class GlTF_PointLight : GlTF_Light {
	public float constantAttenuation = 1f;
	public float linearAttenuation = 0f;
	public float quadraticAttenuation = 0f;
	
	public GlTF_PointLight () { type = "point"; }
	
	public override void Write()
	{
		color.Write();
		Indent();		jsonWriter.Write ("\"constantAttentuation\": "+constantAttenuation);
		Indent();		jsonWriter.Write ("\"linearAttenuation\": "+linearAttenuation);
		Indent();		jsonWriter.Write ("\"quadraticAttenuation\": "+quadraticAttenuation);
		jsonWriter.Write ("}");
	}
}

public class GlTF_SpotLight : GlTF_Light {
	public float constantAttenuation = 1f;
	public float fallOffAngle = 3.1415927f;
	public float fallOffExponent = 0f;
	public float linearAttenuation = 0f;
	public float quadraticAttenuation = 0f;
	
	public GlTF_SpotLight () { type = "spot"; }
	
	public override void Write()
	{
		color.Write();
		Indent();		jsonWriter.Write ("\"constantAttentuation\": "+constantAttenuation);
		Indent();		jsonWriter.Write ("\"fallOffAngle\": "+fallOffAngle);
		Indent();		jsonWriter.Write ("\"fallOffExponent\": "+fallOffExponent);
		Indent();		jsonWriter.Write ("\"linearAttenuation\": "+linearAttenuation);
		Indent();		jsonWriter.Write ("\"quadraticAttenuation\": "+quadraticAttenuation);
		jsonWriter.Write ("}");
	}
}

public class GlTF_Technique : GlTF_Writer {

}

public class GlTF_ColorOrTexture : GlTF_Writer {
	public GlTF_ColorOrTexture() {}
	public GlTF_ColorOrTexture (string n) { name = n; }
}

public class GlTF_MaterialColor : GlTF_ColorOrTexture {
	public GlTF_MaterialColor (string n, Color c) { name = n; color = new GlTF_ColorRGBA(name,c); }
	public GlTF_ColorRGBA color = new GlTF_ColorRGBA ("diffuse");
	public override void Write()
	{
//		Indent();		jsonWriter.Write ("\"" + name + "\": ");
		color.Write ();
	}
}


public class GlTF_MaterialTexture : GlTF_ColorOrTexture {
	public GlTF_MaterialTexture (string n, GlTF_Texture t) { name = n; texture = t; }
	public GlTF_Texture texture;
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": \""+texture.name+"\"");
	}
}

public class GlTF_Material : GlTF_Writer {

	public string instanceTechniqueName = "technique1";
	public GlTF_ColorOrTexture ambient;// = new GlTF_ColorRGBA ("ambient");
	public GlTF_ColorOrTexture diffuse;
	public float shininess;
	public GlTF_ColorOrTexture specular;// = new GlTF_ColorRGBA ("specular");
	
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"instanceTechnique\": {\n");
		IndentIn();
		CommaNL();
		Indent();		jsonWriter.Write ("\"technique\": \"" + instanceTechniqueName + "\",\n");
		Indent();		jsonWriter.Write ("\"values\": {\n");
		IndentIn();
		if (ambient != null)
		{
			CommaNL();
			ambient.Write ();
		}
		if (diffuse != null)
		{
			CommaNL();
			diffuse.Write ();
		}
		CommaNL();
		Indent();		jsonWriter.Write ("\"shininess\": " + shininess);
		if (specular != null)
		{
			CommaNL();
			specular.Write ();
		}
		jsonWriter.WriteLine();
		IndentOut();
		Indent();		jsonWriter.Write ("}");
		jsonWriter.WriteLine();
		IndentOut();
		Indent();		jsonWriter.Write ("},\n");
		Indent();		jsonWriter.Write ("\"name\": \"" + name + "\"\n");
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
	
}

public class GlTF_AnimSampler : GlTF_Writer {
	public string input = "TIME";
	public string interpolation = "LINEAR"; // only things in glTF as of today
	public string output = "translation"; // or whatever
	
	public GlTF_AnimSampler (string n, string o) { name = n; output = o; }
	
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"input\": \"" + input + "\",\n");
		Indent();		jsonWriter.Write ("\"interpolation\": \"" + interpolation + "\",\n");
		Indent();		jsonWriter.Write ("\"output\": \"" + output + "\"\n");
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
}

public class GlTF_Animation : GlTF_Writer {
	public List<GlTF_Channel> channels = new List<GlTF_Channel>();
	public int count;
	public GlTF_Parameters parameters;
	public List<GlTF_AnimSampler> animSamplers = new List<GlTF_AnimSampler>();
	bool gotTranslation = false;
	bool gotRotation = false;
	bool gotScale = false;

	public GlTF_Animation (string n) {
		name = n;
		parameters = new GlTF_Parameters(n);
	}

	public void Populate (AnimationClip c)
	{
	//	AnimationUtility.GetCurveBindings(c);
	// look at each curve
	// if position, rotation, scale detected for first time
	//  create channel, sampler, param for it
	//  populate this curve into proper component
		AnimationClipCurveData[] curveDatas = AnimationUtility.GetAllCurves(c, true);
		if (curveDatas != null)
			count = curveDatas[0].curve.keys.Length;
		for (int i = 0; i < curveDatas.Length; i++)
		{
			string propName = curveDatas[i].propertyName;
			if (propName.Contains("m_LocalPosition"))
			{
				if (!gotTranslation)
				{
					gotTranslation = true;
					GlTF_AnimSampler s = new GlTF_AnimSampler(name+"_AnimSampler", "translation");
					GlTF_Channel ch = new GlTF_Channel("translation", s);
					GlTF_Target target = new GlTF_Target();
					target.id = "FIXTHIS";
					target.path = "translation";
					ch.target = target;
					channels.Add (ch);
					animSamplers.Add (s);
				}
			}
			if (propName.Contains("m_LocalRotation"))
			{
				if (!gotRotation)
				{
					gotRotation = true;
					GlTF_AnimSampler s = new GlTF_AnimSampler(name+"_RotationSampler", "rotation");					
					GlTF_Channel ch = new GlTF_Channel("rotation", s);
					GlTF_Target target = new GlTF_Target();
					target.id = "FIXTHIS";
					target.path = "rotation";
					ch.target = target;
					channels.Add (ch);
					animSamplers.Add (s);
				}
			}
			if (propName.Contains("m_LocalScale"))
			{
				if (!gotScale)
				{
					gotScale = true;
					GlTF_AnimSampler s = new GlTF_AnimSampler(name+"_ScaleSampler", "scale");					
					GlTF_Channel ch = new GlTF_Channel("scale", s);
					GlTF_Target target = new GlTF_Target();
					target.id = "FIXTHIS";
					target.path = "scale";
					ch.target = target;
					channels.Add (ch);
					animSamplers.Add (s);
				}
			}
			parameters.Populate (curveDatas[i]);
			//			Type propType = curveDatas[i].type;
		}
	}
		
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"channels\": [\n");
		foreach (GlTF_Channel c in channels)
		{
			CommaNL();
			c.Write ();
		}
		jsonWriter.WriteLine();
		Indent();		jsonWriter.Write ("]");
		CommaNL();

		Indent();		jsonWriter.Write ("\"count\": "+ count +",\n");
		
		parameters.Write ();
		CommaNL();
		
		Indent();		jsonWriter.Write ("\"samplers\": {\n");
		IndentIn();
		foreach (GlTF_AnimSampler s in animSamplers)
		{
			CommaNL();
			s.Write ();
		}
		IndentOut();
		jsonWriter.WriteLine();
		Indent();		jsonWriter.Write ("}\n");

		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
}

public class GlTF_Channel : GlTF_Writer {
	public GlTF_AnimSampler sampler;
	public GlTF_Target target;

	public GlTF_Channel (string ch, GlTF_AnimSampler s) {
		sampler = s;
		switch (ch)
		{
			case "translation":
				break;
			case "rotation":
				break;
			case "scale":
				break;
		}
	}

	public override void Write()
	{
		IndentIn();
		Indent();		jsonWriter.Write ("{\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"sampler\": \"" + sampler.name + "\",\n");
		target.Write ();
		jsonWriter.WriteLine();
		IndentOut();
		Indent();		jsonWriter.Write ("}");
		IndentOut();
	}
}

public class GlTF_Target : GlTF_Writer {
	public string id;
	public string path;
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + "target" + "\": {\n");
//		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		Indent();		jsonWriter.Write ("\"id\": \"" + id + "\",\n");
		Indent();		jsonWriter.Write ("\"path\": \"" + path + "\"\n");
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
}

public class GlTF_Parameters : GlTF_Writer {
	public GlTF_Accessor timeAccessor;
	public GlTF_Accessor translationAccessor;
	public GlTF_Accessor rotationAccessor;
	public GlTF_Accessor scaleAccessor;

	// seems like a bad place for this
	float[] times;// = new float[curve.keys.Length];
	Vector3[] positions;
	Vector3[] scales;
	Vector4[] rotations;
	bool px, py, pz;
	bool sx, sy, sz;
	bool rx, ry, rz, rw;

	public GlTF_Parameters (string n) { name = n; }

	public void Populate (AnimationClipCurveData curveData)
	{
		string propName = curveData.propertyName;
		if (times == null) // allocate one array of times, assumes all channels have same number of keys
		{
			timeAccessor = new GlTF_Accessor(name+"TimeAccessor", "SCALAR", "FLOAT");
			timeAccessor.bufferView = GlTF_Writer.floatBufferView;
			GlTF_Writer.accessors.Add (timeAccessor);
			times = new float[curveData.curve.keys.Length];
			for (int i = 0; i < curveData.curve.keys.Length; i++)
				times[i] = curveData.curve.keys[i].time;
			timeAccessor.Populate (times);
		}

		if (propName.Contains("m_LocalPosition"))
		{
			if (positions == null)
			{
				translationAccessor = new GlTF_Accessor(name+"TranslationAccessor", "VEC3", "FLOAT");
				translationAccessor.bufferView = GlTF_Writer.vec3BufferView;
				GlTF_Writer.accessors.Add (translationAccessor);
				positions = new Vector3[curveData.curve.keys.Length];
			}

			if (propName.Contains (".x"))
		    {
		    	px = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					positions[i].x = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".y"))
			{
				py = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					positions[i].y = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".z"))
			{
				pz = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					positions[i].z = curveData.curve.keys[i].value;
			}
			if (px && py && pz)
				translationAccessor.Populate (positions);
		}
		
		if (propName.Contains("m_LocalScale"))
		{
			if (scales == null)
			{
				scaleAccessor = new GlTF_Accessor(name+"ScaleAccessor", "VEC3", "FLOAT");
				scaleAccessor.bufferView = GlTF_Writer.vec3BufferView;
				GlTF_Writer.accessors.Add (scaleAccessor);
				scales = new Vector3[curveData.curve.keys.Length];
			}
			
			if (propName.Contains (".x"))
			{
				sx = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					scales[i].x = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".y"))
			{
				sy = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					scales[i].y = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".z"))
			{
				sz = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					scales[i].z = curveData.curve.keys[i].value;
			}
			if (sx && sy && sz)
				scaleAccessor.Populate (scales);
		}

		if (propName.Contains("m_LocalRotation"))
		{
			if (rotations == null)
			{
				rotationAccessor = new GlTF_Accessor(name+"RotationAccessor", "VEC4", "FLOAT");
				rotationAccessor.bufferView = GlTF_Writer.vec4BufferView;
				GlTF_Writer.accessors.Add (rotationAccessor);
				rotations = new Vector4[curveData.curve.keys.Length];
			}
			
			if (propName.Contains (".x"))
			{
				rx = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					rotations[i].x = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".y"))
			{
				ry = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					rotations[i].y = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".z"))
			{
				rz = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					rotations[i].z = curveData.curve.keys[i].value;
			}
			else if (propName.Contains (".w"))
			{
				rw = true;
				for (int i = 0; i < curveData.curve.keys.Length; i++)
					rotations[i].w = curveData.curve.keys[i].value;
			}
			if (rx && ry && rz && rw)
				rotationAccessor.Populate (scales);
		}
	}

	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + "parameters" + "\": {\n");
		IndentIn();
		if (times != null)
		{
			CommaNL();
			Indent();		jsonWriter.Write ("\"" + "TIME" + "\": \"" + timeAccessor.name +"\"");
		}
		if (rotations != null)
		{
			CommaNL();
			Indent();		jsonWriter.Write ("\"" + "rotation" + "\": \"" + rotationAccessor.name +"\"");
		}
		if (scales != null)
		{
			CommaNL();
			Indent();		jsonWriter.Write ("\"" + "scale" + "\": \"" + scaleAccessor.name +"\"");
		}
		if (positions != null)
		{
			CommaNL();
			Indent();		jsonWriter.Write ("\"" + "translation" + "\": \"" + translationAccessor.name +"\"");
		}
		jsonWriter.WriteLine();		
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}

		/*
	public Dictionary<string, string> parms = new Dictionary<string,string>();

	public void AddPararm (string key, string val)
	{
		parms.Add (key, val);
	}

	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		foreach (KeyValuePair<string,string> p in parms)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"" + p.Key + "\": \"" + p.Value +"\"");
		}
		jsonWriter.WriteLine();
		
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
*/
}

public class GlTF_Node : GlTF_Writer {
	public string cameraName;
	public bool hasParent = false;
	public List<string> childrenNames = new List<string>();
	public bool uniqueItems = true;
	public string lightName;
	public List<string>bufferViewNames = new List<string>();
	public List<string>indexNames = new List<string>();
	public List<string>accessorNames = new List<string>();
	public List<string> meshNames = new List<string>();
	public GlTF_Matrix matrix;
//	public GlTF_Mesh mesh;
	public GlTF_Rotation rotation;
	public GlTF_Scale scale;
	public GlTF_Translation translation;
	public bool additionalProperties = false;
	
	public override void Write ()
	{
		Indent();
		jsonWriter.Write ("\"node-"+name+"\": {\n");
		IndentIn();
		Indent();
		jsonWriter.Write ("\"name\": \""+name+"\",\n");
		Indent();
		if (cameraName != null)
		{
			CommaStart();
			jsonWriter.Write ("\"camera\": \""+cameraName+"\"");
		}
		else if (lightName != null)
		{
			CommaStart();
			jsonWriter.Write ("\"light\": \""+lightName+"\"");
		}
		else if (meshNames.Count > 0)
		{
			CommaStart();
			jsonWriter.Write ("\"meshes\": [\n");
			IndentIn();
			foreach (string m in meshNames)
			{
				CommaNL();
				Indent();	jsonWriter.Write ("\"" + m + "\"");
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("]");
		}
		
		if (childrenNames != null && childrenNames.Count > 0)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"children\": [\n");
			IndentIn();
			foreach (string ch in childrenNames)
			{
				CommaNL();
				Indent();		jsonWriter.Write ("\""+ch+"\"");
			}
			jsonWriter.WriteLine();
			IndentOut();
			Indent();	jsonWriter.Write ("]");
		}
		else
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"children\": []");
		}
		if (translation != null && (translation.items[0] != 0f || translation.items[1] != 0f || translation.items[2] != 0f))
		{
			CommaNL();
			translation.Write ();
		}
		if (scale != null && (scale.items[0] != 1f || scale.items[1] != 1f || scale.items[2] != 1f))
		{
			CommaNL();
			scale.Write();
		}
		if (rotation != null && (rotation.items[0] != 0f || rotation.items[1] != 0f || rotation.items[2] != 0f || rotation.items[3] != 0f))
		{
			CommaNL();
			rotation.Write ();
		}
		jsonWriter.WriteLine();
		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
}