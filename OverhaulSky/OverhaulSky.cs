using UnityEngine;
using HarmonyLib;
using Bindito.Core;
using Timberborn.CameraSystem;
using Timberborn.SingletonSystem;
using Timberborn.Rendering;
using Timberborn.MapStateSystem;
using Timberborn.ModManagerScene;
using Timberborn.SkySystem;
using Timberborn.TimeSystem;
using System.Reflection;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements.Collections;
using UnityEngine.Rendering;

public class OverhaulSky : IModStarter {
	public void StartMod(IModEnvironment env) {
		Debug.Log(GetType().Name);
		var harmony = new Harmony("Robin.OverhaulSky");
		harmony.PatchAll();
	}
}

[Context("Game")]
[Context("MapEditor")]
class SkyConfigurator : IConfigurator {
	public void Configure(IContainerDefinition c) {
		Debug.Log(GetType().Name);
		try { c.Bind<Cam>().AsSingleton(); } catch { }
		c.Bind<Sky>().AsSingleton();
	}
}

class Sky(
	//Cam cam,
	CameraService cameraService,
	Sun sunService,
	MapSize mapSize,
	DayStageCycle dayStageCycle
) : ILoadableSingleton, ILateUpdatableSingleton {
	GameObject upCrosshair = Utility.crosshair(
		PrimitiveType.Cylinder,
		Color.violet,
		transform => {
			transform.localScale = new Vector3(0.1f, 100, 0.1f);
			transform.localPosition = new Vector3(0, 100, 0);
		}
	);
	GameObject geographicNorthCrosshair = Utility.crosshair(
		PrimitiveType.Cylinder,
		Color.red,
		transform => {
			transform.localScale = new Vector3(0.1f, 100, 0.1f);
			transform.localPosition = new Vector3(0, 100, 0);
		}
	);
	GameObject planetaryNorthCrosshair = Utility.crosshair(
		PrimitiveType.Cylinder,
		Color.orange,
		transform => {
			transform.localScale = new Vector3(0.1f, 100, 0.1f);
			transform.localPosition = new Vector3(0, 100, 0);
		}
	);
	GameObject solarRotationCrosshair = Utility.crosshair(
		PrimitiveType.Cylinder,
		Color.yellow,
		transform => {
			transform.localScale = new Vector3(0.1f, 100, 0.1f);
			transform.localPosition = new Vector3(0, 100, 0);
		}
	);
	GameObject lunarRotationCrosshair = Utility.crosshair(
		PrimitiveType.Cylinder,
		Color.green,
		transform => {
			transform.localScale = new Vector3(0.1f, 100, 0.1f);
			transform.localPosition = new Vector3(0, 100, 0);
		}
	);
	GameObject sun = null!;
	GameObject moon = null!;
	Material star_material = null!;
	Material constellation_material = null!;
	readonly GameObject star_empty = new();
	readonly List<GameObject> star_list = [];
	readonly List<GameObject> line_list = [];
	readonly static int SKY_LAYER = 14;
	public void Load() {
		Debug.Log("Sky.Load");

		sun = Icosphere.Create(3, 1);
		sun.layer = Layers.IgnoreRaycastMask;
		var sunMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		sunMaterial.color = new Color(230 / 255f, 220 / 255f, 140 / 255f);
		sun.AddComponent<MeshRenderer>().material = sunMaterial;
		sun.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
		sun.GetComponent<MeshRenderer>().receiveShadows = false;
		sun.layer = SKY_LAYER;
		sun.transform.localScale = new Vector3(30f, 30f, 30f);

		moon = Icosphere.Create(4, 0.51f, Quaternion.Euler(0, 0, tiltAngle));
		moon.layer = Layers.IgnoreRaycastMask;
		var moonMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		moonMaterial.color = new Color(230 / 255f, 220 / 255f, 200 / 255f);
		var moon_stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OverhaulSky.moon.jpg");
		moonMaterial.mainTexture = Utility.texture(moon_stream);
		moon.AddComponent<MeshRenderer>().material = moonMaterial;
		moon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
		moon.GetComponent<MeshRenderer>().receiveShadows = false;
		moon.layer = SKY_LAYER;
		moon.transform.localScale = new Vector3(22.5f, 22.5f, 22.5f);

		star_material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		star_material.color = new Color(255 / 255f, 255 / 255f, 255 / 255f, 1f);
		star_material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		star_material.SetFloat("_Surface", 1);
		star_material.SetFloat("_Blend", 0);
		star_material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
		star_material.SetFloat("_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
		star_material.SetFloat("_ZWrite", 0);
		star_material.renderQueue = (int) RenderQueue.Transparent;

		var star_map = new Dictionary<int, Vector3>();

		Debug.Log("the stars");
		var stars_stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OverhaulSky.bsc5.json");
		var stars_json = JArray.Load(new JsonTextReader(new StreamReader(stars_stream)));
		foreach (JObject star_json in stars_json) {
			var hr = int.Parse(star_json.Value<string>("HR")!);
			var dec = Utility.DmsToDeg(star_json.Value<string>("Dec")!);
			var ra = Utility.HmsToDeg(star_json.Value<string>("RA")!);
			var pm_dec = float.Parse(star_json.Value<string>("pmDE")!) / 3600f;
			var pm_ra = float.Parse(star_json.Value<string>("pmRA")!) / 3600f;
			var vmag = float.Parse(star_json.Value<string>("Vmag")!);
			var star = GameObject.CreatePrimitive(PrimitiveType.Cube);
			int YEARS_IN_FUTURE = 20 * 1000;
			var vector = Quaternion.Euler(dec + pm_dec * YEARS_IN_FUTURE, ra + pm_ra * YEARS_IN_FUTURE, 0);
			star.transform.localPosition = vector * Vector3.forward * 1200f;
			if (hr > 0) {
				star_map.Add(hr, star.transform.localPosition);
			}
			//star.transform.localScale = Vector3.one * (float) Math.Max(0, Math.Pow(10, -0.4 * vmag)) * 100f;
			star.transform.localScale = Vector3.one * (float) Math.Max(0, 7f - vmag) * 1.1f;
			star.GetComponent<Renderer>().material = star_material;
			star.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
			star.GetComponent<Renderer>().receiveShadows = false;
			star.layer = SKY_LAYER;
			star.transform.parent = star_empty.transform;
			star_list.Add(star);
			/*if (star_json.Value<string>("ADS") == "1477") {
				var special_material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
				special_material.color = new Color(255 / 255f, 0 / 255f, 0 / 255f);
				star.GetComponent<Renderer>().material = special_material;
				star.transform.localScale = Vector3.one * 25f;
			}*/
		}

		constellation_material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		constellation_material.color = new Color(255 / 255f, 255 / 255f, 255 / 255f, 0.2f);
		constellation_material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		constellation_material.SetFloat("_Surface", 1);
		constellation_material.SetFloat("_Blend", 0);
		constellation_material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
		constellation_material.SetFloat("_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
		constellation_material.SetFloat("_ZWrite", 0);
		constellation_material.renderQueue = (int) RenderQueue.Transparent;

		Debug.Log("the constellations");
		var consts_stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OverhaulSky.const.json");
		var consts_json = JArray.Load(new JsonTextReader(new StreamReader(consts_stream)));
		foreach (var const_json in consts_json) {
			var point_list = const_json.Value<JArray>("line")!;
			int? last_number = null;
			foreach (var point in point_list) {
				var number = point.Value<int>();
				if (last_number != null) {
					var position = star_map.Get((int) last_number)!;
					var next_position = star_map.Get(number)!;
					var rotation = Quaternion.FromToRotation(Vector3.left, next_position - position);
					var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
					var length = Vector3.Distance(position, next_position);
					float GAP = 15;
					line.transform.localPosition = position + rotation * Vector3.left * length / 2f;
					line.transform.localRotation = rotation;
					line.transform.localScale = new Vector3(length - GAP * 2, 1f, 1f);
					line.GetComponent<Renderer>().material = constellation_material;
					line.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
					line.GetComponent<Renderer>().receiveShadows = false;
					line.layer = SKY_LAYER;
					line.transform.parent = star_empty.transform;
					line_list.Add(line);
				}
				last_number = number;
			}
			sunService._sun.cullingMask &= ~(1 << SKY_LAYER);
			sunService._sun.renderingLayerMask &= ~(1 << SKY_LAYER);
		}

		Debug.Log($"fov {cameraService._camera.fieldOfView}");
		cameraService._camera.fieldOfView = 70f;
	}

	public void LateUpdateSingleton() {
		Render();
	}

	readonly DayNightCycle dayNightCycle = (DayNightCycle) dayStageCycle._dayNightCycle;
	readonly int tiltAngle = 30;
	readonly int latitudeAngle = 50;
	// assume permanant summer solstice lol

	void Render() {
		var camera_position = cameraService.Transform.position;
		var map_center = new Vector3(mapSize.TerrainSize.x * 0.5f, 0, mapSize.TerrainSize.y * 0.5f);

		var dayProgress = (
			dayNightCycle.DayNumber +
			dayNightCycle.FluidSecondsPassedToday / dayNightCycle.DayLengthInSeconds
		);
		//dayProgress *= 30;

		var solarAngle = (dayProgress + 3.5f / 24f) * 360f;

		var up = Quaternion.LookRotation(Vector3.up);
		upCrosshair.transform.localPosition = map_center;
		upCrosshair.transform.localRotation = up * Quaternion.Euler(90, 0, 0);

		var geographicNorth = Quaternion.LookRotation(Vector3.forward);
		geographicNorthCrosshair.transform.localPosition = map_center;
		geographicNorthCrosshair.transform.localRotation = geographicNorth * Quaternion.Euler(90, 0, 0);

		var planetaryNorth = geographicNorth * Quaternion.Euler(0 - latitudeAngle, 0, 0);
		planetaryNorthCrosshair.transform.localPosition = map_center;
		planetaryNorthCrosshair.transform.localRotation = planetaryNorth * Quaternion.Euler(90, 0, 0);

		var solarRotation = planetaryNorth * Quaternion.Euler(0, 0, solarAngle) * Quaternion.Euler(90 - tiltAngle, 0, 0);
		solarRotationCrosshair.transform.localPosition = map_center;
		solarRotationCrosshair.transform.localRotation = solarRotation * Quaternion.Euler(90, 0, 0);
		var sunVector = solarRotation * Vector3.forward;

		var lunarAngle = solarAngle * 29 / 28 + 180;
		//lunarAngle *= 3;
		var lunarRotation = planetaryNorth * Quaternion.Euler(0, 0, lunarAngle) * Quaternion.Euler(90, 0, 0);
		lunarRotationCrosshair.transform.localPosition = map_center;
		lunarRotationCrosshair.transform.localRotation = lunarRotation * Quaternion.Euler(90, 0, 0);
		var moonVector = lunarRotation * Vector3.forward;

		sun.transform.localRotation = solarRotation * Quaternion.Euler(0, 90, 0);
		sun.transform.localPosition = camera_position + sunVector * 800f;
		moon.transform.localPosition = camera_position + moonVector * 600f;
		moon.transform.localRotation = solarRotation * Quaternion.Euler(0, 0 - 90, 0);
		moon.GetComponent<MeshRenderer>().material.mainTextureOffset = (
			new Vector2((lunarAngle - solarAngle) / 360 + 0.5f, 0)
		);

		var star_angle = solarAngle * 364 / 365;
		star_empty.transform.localPosition = camera_position;
		star_empty.transform.localRotation = planetaryNorth * Quaternion.Euler(0, 0, star_angle) * Quaternion.Euler(-90, 0, 0);

		var transition = sunService._dayStageCycle.GetCurrentTransition();
		sunService.UpdateColors(transition);

		var sunRelevance = Mathf.Clamp(sunVector.y * 10, 0, 1);
		sunService._sun.intensity *= sunRelevance;
		sunService._sun.transform.localRotation = Quaternion.LookRotation(Vector3.zero - sunVector);
		/*
		} else if (moonVector.y > 0) {
			var moonRelevance = (
				Vector3.Angle(sunVector, moonVector) / 180 *
				Mathf.Clamp(0 - sunVector.y * 10, 0, 1)
			);
			sunService._sun.intensity = moonRelevance * 0.5f * 0f;
			sunService._sun.transform.localRotation = Quaternion.LookRotation(Vector3.zero - moonVector);
			sunService._sun.color = Color.white;
		} else {
			sunService._sun.intensity = 0;
		}
		*/

		star_material.color = new Color(255 / 255f, 255 / 255f, 255 / 255f, 1f - 0.95f * sunRelevance);
		constellation_material.color = new Color(255 / 255f, 255 / 255f, 255 / 255f, 0.1f - 0.05f * sunRelevance);
	}
}

[HarmonyPatch]
class SkyPatch {
	// hide default stars
	[HarmonyPostfix, HarmonyPatch(typeof(SkyboxPositioner), nameof(SkyboxPositioner.Load))]
	static void SkyboxPositionerLoad(SkyboxPositioner __instance) {
		Debug.Log(Shader.PropertyToID("_StarIntensity"));
		__instance.SkyboxMaterial.SetFloat(Shader.PropertyToID("_StarSize"), 0);
	}

	// turn off default sun motion
	[HarmonyPrefix, HarmonyPatch(typeof(Sun), nameof(Sun.UpdateColorsAndRotation))]
	static bool UpdateColorsAndRotation() {
		return false;
	}
}
