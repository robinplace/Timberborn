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
using System.Linq;
using Timberborn.Common;

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
	CameraService camera_service,
	Sun sun_service,
	MapSize map_size,
	DayStageCycle day_stage_cycle
) : ILoadableSingleton, ILateUpdatableSingleton {
	readonly GameObject up_ray = Utility.ray(Color.violet);
	readonly GameObject pole_ray = Utility.ray(Color.orange);
	readonly GameObject sun_ray = Utility.ray(Color.yellow);
	readonly GameObject moon_ray = Utility.ray(Color.green);
	readonly GameObject light_ray = Utility.ray(Color.red, 0.9f);

	readonly DayNightCycle day_night_cycle = (DayNightCycle) day_stage_cycle._dayNightCycle;

	readonly static int TILT_ANGLE = 30;
	readonly static int LATITUDE_ANGLE = 50;
	// assume permanant summer solstice lol

	// positionor for camera
	readonly GameObject positionor = new();

	// orientor for celestial pole
	readonly GameObject orientor = new();

	// rotator for each celestial body
	readonly GameObject sun_rotator = new();
	readonly GameObject moon_rotator = new();
	readonly GameObject star_rotator = new();

	// visible object for each celestial body
	readonly GameObject sun = Icosphere.Create(3, 1);
	readonly GameObject moon = Icosphere.Create(4, 0.51f, Quaternion.Euler(0, 0, TILT_ANGLE));
	readonly List<GameObject> star_list = [];
	readonly List<GameObject> line_list = [];

	// rendering materials, etc
	readonly Material star_material = new(Shader.Find("Universal Render Pipeline/Unlit"));
	readonly Material constellation_material = new(Shader.Find("Universal Render Pipeline/Unlit"));
	readonly static int SKY_LAYER = 14;

	// day calculation
	public float DAY_SECONDS => day_night_cycle.DayLengthInSeconds;
	public float days_elapsed => (
		day_night_cycle.DayNumber +
		day_night_cycle.FluidSecondsPassedToday / day_night_cycle.DayLengthInSeconds
	);

	public void Load() {
		Debug.Log("Sky.Load");

		up_ray.transform.SetParent(positionor.transform, false);

		var pole_rotation = Quaternion.Euler(90 - LATITUDE_ANGLE, 0, 0);
		orientor.transform.SetParent(positionor.transform, false);
		orientor.transform.localRotation = pole_rotation;
		pole_ray.transform.SetParent(orientor.transform, false);

		var sky_time = days_elapsed + 3.5f / 24f;

		var SUN_PERIOD = DAY_SECONDS;
		sun_rotator.transform.SetParent(orientor.transform, false);
		var sun_rotator_animation = sun_rotator.AddComponent<Animation>();
		var sun_rotator_clip = new AnimationClip();
		sun_rotator_clip.wrapMode = WrapMode.Loop;
		sun_rotator_clip.legacy = true;
		var sun_rotator_curve = AnimationCurve.Linear(0f, 0f, SUN_PERIOD, 360f);
		sun_rotator_clip.SetCurve("", typeof(Transform), "localEulerAngles.y", sun_rotator_curve);
		sun_rotator_animation.AddClip(sun_rotator_clip, "rotate");
		sun_rotator_animation.Play("rotate");
		sun_rotator_animation["rotate"].time = sky_time;
		sun_ray.transform.SetParent(sun_rotator.transform, false);
		sun_ray.transform.localRotation = Quaternion.Euler(90 - TILT_ANGLE, 0, 0);

		var MOON_PERIOD = DAY_SECONDS * 9 / 10; // a month is 10 days
		moon_rotator.transform.SetParent(orientor.transform, false);
		var moon_rotator_animation = moon_rotator.AddComponent<Animation>();
		var moon_rotator_clip = new AnimationClip();
		moon_rotator_clip.wrapMode = WrapMode.Loop;
		moon_rotator_clip.legacy = true;
		var moon_rotator_curve = AnimationCurve.Linear(0f, 0f, MOON_PERIOD, 360f);
		moon_rotator_clip.SetCurve("", typeof(Transform), "localEulerAngles.y", moon_rotator_curve);
		moon_rotator_animation.AddClip(moon_rotator_clip, "rotate");
		moon_rotator_animation.Play("rotate");
		moon_rotator_animation["rotate"].time = sky_time + MOON_PERIOD / 2; // start mid-month
		moon_ray.transform.SetParent(moon_rotator.transform, false);
		moon_ray.transform.localRotation = Quaternion.Euler(90, 0, 0);

		var STAR_PERIOD = DAY_SECONDS * 119 / 120; // a year is 120 days
		star_rotator.transform.SetParent(orientor.transform, false);
		var star_rotator_animation = star_rotator.AddComponent<Animation>();
		var star_rotator_clip = new AnimationClip();
		star_rotator_clip.wrapMode = WrapMode.Loop;
		star_rotator_clip.legacy = true;
		var star_rotator_curve = AnimationCurve.Linear(0f, 0f, STAR_PERIOD, 360f);
		star_rotator_clip.SetCurve("", typeof(Transform), "localEulerAngles.y", star_rotator_curve);
		star_rotator_animation.AddClip(star_rotator_clip, "rotate");
		star_rotator_animation.Play("rotate");
		star_rotator_animation["rotate"].time = sky_time;

		light_ray.transform.SetParent(positionor.transform, false);

		var star_map = new Dictionary<int, Vector3>();

		Debug.Log("the stars");
		var stars_stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OverhaulSky.bsc5.json");
		var stars_json = JArray.Load(new JsonTextReader(new StreamReader(stars_stream)));
		foreach (var star_json in stars_json.Cast<JObject>()) {
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
			star.transform.SetParent(star_rotator.transform, false);
			star_list.Add(star);
			/*if (star_json.Value<string>("ADS") == "1477") {
				var special_material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
				special_material.color = new Color(1, 0 / 255f, 0 / 255f);
				star.GetComponent<Renderer>().material = special_material;
				star.transform.localScale = Vector3.one * 25f;
			}*/
		}

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
					line.transform.SetParent(star_rotator.transform, false);
					line_list.Add(line);
				}
				last_number = number;
			}
		}

		Debug.Log($"fov {camera_service._camera.fieldOfView}");
		camera_service._camera.fieldOfView = 50f;

		sun.layer = Layers.IgnoreRaycastMask;
		var sunMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		sunMaterial.color = new Color(230 / 255f, 220 / 255f, 140 / 255f);
		sun.AddComponent<MeshRenderer>().material = sunMaterial;
		sun.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
		sun.GetComponent<MeshRenderer>().receiveShadows = false;
		sun.layer = SKY_LAYER;
		sun.transform.localScale = new Vector3(30f, 30f, 30f);
		sun.transform.SetParent(sun_rotator.transform, false);
		sun.transform.localPosition = sun_ray.transform.localRotation * Vector3.up * 800f / 3;

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
		moon.transform.SetParent(moon_rotator.transform, false);
		moon.transform.localPosition = moon_ray.transform.localRotation * Vector3.up * 600f / 3;

		var MOON_PHASE_PERIOD = SUN_PERIOD * MOON_PERIOD / (SUN_PERIOD - MOON_PERIOD);
		var moon_animation = moon.AddComponent<Animation>();
		var moon_rotate_clip = new AnimationClip();
		moon_rotate_clip.wrapMode = WrapMode.Loop;
		moon_rotate_clip.legacy = true;
		var moon_rotate_curve = AnimationCurve.Linear(0f, 0f, MOON_PHASE_PERIOD, -360f);
		moon_rotate_clip.SetCurve("", typeof(Transform), "localEulerAngles.y", moon_rotate_curve);
		moon_animation.AddClip(moon_rotate_clip, "rotate");
		moon_animation.Play("rotate");
		moon_animation["rotate"].time = sky_time - MOON_PHASE_PERIOD / 4;
		/*var moon_texture_clip = new AnimationClip();
		moon_texture_clip.wrapMode = WrapMode.Loop;
		moon_texture_clip.legacy = true;
		var moon_texture_curve = AnimationCurve.Linear(0f, 0f, MOON_PHASE_PERIOD, 1f);
		moon_texture_clip.SetCurve("", typeof(MeshRenderer), "material.mainTextureOffset.x", moon_texture_curve);
		moon_animation.AddClip(moon_texture_clip, "texture");
		moon_animation.Play("texture");
		moon_animation["texture"].time = sky_time - MOON_PHASE_PERIOD / 4;*/

		star_material.color = new Color(1, 1, 1, 1f);
		star_material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		star_material.SetFloat("_Surface", 1);
		star_material.SetFloat("_Blend", 0);
		star_material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
		star_material.SetFloat("_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
		star_material.SetFloat("_ZWrite", 0);
		star_material.renderQueue = (int) RenderQueue.Transparent;

		constellation_material.color = new Color(1, 1, 1, 0.1f);
		constellation_material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		constellation_material.SetFloat("_Surface", 1);
		constellation_material.SetFloat("_Blend", 0);
		constellation_material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
		constellation_material.SetFloat("_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
		constellation_material.SetFloat("_ZWrite", 0);
		constellation_material.renderQueue = (int) RenderQueue.Transparent;

		sun_service._sun.cullingMask &= ~(1 << SKY_LAYER);
		sun_service._sun.renderingLayerMask &= ~(1 << SKY_LAYER);
	}

	public void LateUpdateSingleton() {
		if (Utility.DEBUG) {
			positionor.transform.localPosition = new Vector3(
				map_size.TerrainSize.x * 0.5f,
				15,
				map_size.TerrainSize.y * 0.5f
			);
		} else {
			positionor.transform.localPosition = camera_service.Transform.position;
		}

		var transition = sun_service._dayStageCycle.GetCurrentTransition();
		sun_service.UpdateColors(transition);

		var sun_rotation = (
			orientor.transform.localRotation *
			sun_rotator.transform.localRotation *
			Quaternion.Euler(90 - TILT_ANGLE, 0, 0)
		);
		var sun_relevance = Mathf.Clamp01((sun_rotation * Vector3.up).y * 20f);
		sun_service._sun.intensity = sun_relevance;
		sun_service._sun.transform.localRotation = sun_rotation * Quaternion.Euler(90, 0, 0);
		light_ray.transform.localRotation = sun_rotation;
		light_ray.transform.localScale = Vector3.one * 0.5f;
		/*} else if (moonVector.y > 0) {
			var moonRelevance = (
				Vector3.Angle(sunVector, moonVector) / 180 *
				Mathf.Clamp(0 - sunVector.y * 10, 0, 1)
			);
			sunService._sun.intensity = moonRelevance * 0.5f * 0f;
			sunService._sun.transform.localRotation = Quaternion.LookRotation(Vector3.zero - moonVector);
			sunService._sun.color = Color.white;
		} else {
			sunService._sun.intensity = 0;
		}*/

		star_material.color = new Color(1, 1, 1, Mathf.Lerp(1, 0.1f, sun_relevance));
		constellation_material.color = new Color(1, 1, 1, Mathf.Lerp(0.1f, 0.05f, sun_relevance));
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
