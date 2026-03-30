using UnityEngine;
using Timberborn.CameraSystem;
using Timberborn.SingletonSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Rendering;
using Timberborn.MapStateSystem;
using System.IO;
using System.Text.RegularExpressions;
using System;

public class Utility {
	public static bool DEBUG = false;

	public delegate void Transformer(Transform transform);

	public static float DmsToDeg(string s) {
		// +45° 13′ 45″
		var match = Regex.Match(s, @"^\+?(-?\d+)° (\d+)′ ([\d\.]+)″$", RegexOptions.IgnoreCase);
		float deg = float.Parse(match.Groups[1].Value);
		float min = float.Parse(match.Groups[2].Value);
		float sec = float.Parse(match.Groups[3].Value);
		return (deg < 0 ? -1f : 1f) * (Math.Abs(deg) + min / 60f + sec / 3600f);
	}

	public static float HmsToDeg(string s) {
		// 00h 08m 12.1s
		var match = Regex.Match(s, @"^\+?(-?\d+)h (\d+)m ([\d\.]+)s$", RegexOptions.IgnoreCase);
		float hou = float.Parse(match.Groups[1].Value);
		float min = float.Parse(match.Groups[2].Value);
		float sec = float.Parse(match.Groups[3].Value);
		return 15f * (hou < 0 ? -1f : 1f) * (Math.Abs(hou) + min / 60f + sec / 3600f);
	}

	public static GameObject crosshair(
		PrimitiveType? type = null,
		Color? color = null,
		Transformer? transformer = null
	) {
		var crosshair = GameObject.CreatePrimitive(type ?? PrimitiveType.Sphere);
		crosshair.SetActive(DEBUG);
		crosshair.layer = Layers.IgnoreRaycastMask;
		if (transformer != null) {
			transformer(crosshair.transform);
		}
		var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		material.color = color ?? Color.magenta;
		crosshair.GetComponent<Renderer>().material = material;
		var container = new GameObject();
		crosshair.transform.parent = container.transform;
		return container;
	}

	public static GameObject ray(
		Color? color = null,
		float? thickness = null
	) {
		return crosshair(
			PrimitiveType.Cylinder,
			color,
			transform => {
				transform.localScale = new Vector3(thickness ?? 0.3f, 100, thickness ?? 0.3f);
				transform.localPosition = new Vector3(0, 100, 0);
			}
		);
	}

	public static Texture2D texture(
			Stream stream
	) {
		var ms = new MemoryStream();
		stream.CopyTo(ms);
		var tex = new Texture2D(1, 1);
		tex.LoadImage(ms.ToArray());
		stream.Dispose();
		return tex;
	}
}

public class Cam(
	CameraService cameraService,
	ISpecService specService,
	MapSize mapSize
) : ILoadableSingleton, ILateUpdatableSingleton {
	CameraServiceSpec cameraServiceSpec = null!;
	GameObject crosshair = Utility.crosshair(PrimitiveType.Sphere, Color.white);
	GameObject ground = null!;

	public void Load() {
		Debug.Log("Cam.Load");
		cameraServiceSpec = specService.GetSingleSpec<CameraServiceSpec>();
		cameraService._camera.farClipPlane = 2 * 1000f;
		RenderSettings.fog = false;
		cameraService.FreeMode = true;

		ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
		ground.layer = Layers.IgnoreRaycastMask;
		var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
		material.color = new Color(42 / 255f, 40 / 255f, 34 / 255f);
		ground.GetComponent<Renderer>().material = material;
		ground.transform.localRotation = Quaternion.Euler(0 - 90, 0, 0);
	}

	public void LateUpdateSingleton() {
		ground.transform.localPosition = new Vector3(mapSize.TerrainSize.x / 2, 0 - 1.15f, mapSize.TerrainSize.y / 2);
		ground.transform.localScale = new Vector3(mapSize.TerrainSize.x, mapSize.TerrainSize.y, 1);
	}

	public Camera camera => cameraService._camera;
	public float distance {
		get => (
			Mathf.Pow(cameraServiceSpec!.ZoomBase, cameraService.ZoomLevel) *
			cameraServiceSpec!.BaseDistance
		);
	}
	public Quaternion rotation {
		get => Quaternion.Euler(
			cameraService.VerticalAngle,
			cameraService.HorizontalAngle,
			0
		);
		set {
			//Debug.Log("value " + value * Vector3.forward);
			//Debug.Log("angle " + (Vector3.Angle(value * Vector3.forward, Vector3.up) - 90));
			cameraService.VerticalAngle = Vector3.Angle(value * Vector3.forward, Vector3.up) - 90;
			cameraService.HorizontalAngle = value.eulerAngles.y;
		}
	}
	public Vector3 position {
		get => cameraService.Target + rotation * Vector3.back * distance;
		set {
			var mapCenter = new Vector3(mapSize.TerrainSize.x * 0.5f, 0, mapSize.TerrainSize.y * 0.5f);
			var ray = new Ray(value, rotation * Vector3.forward);

			var planeArray = new Plane[] {
				new Plane(Vector3.up, 0),
				new Plane(Vector3.left, 0 - cameraServiceSpec.FreeModeMapMargin),
				new Plane(Vector3.left, mapSize.TerrainSize.x + cameraServiceSpec.FreeModeMapMargin),
				new Plane(Vector3.back, 0 - cameraServiceSpec.FreeModeMapMargin),
				new Plane(Vector3.back, mapSize.TerrainSize.y + cameraServiceSpec.FreeModeMapMargin)
			};

			var minimumDistance = float.PositiveInfinity;
			var mostCentralPoint = mapCenter;
			var mostCentralPointOffset = 20f;
			for (var i = 0; i < planeArray.Length; i++) {
				if (planeArray[i].Raycast(ray, out var offset)) {
					var point = ray.GetPoint(offset);
					var distance = Vector3.Distance(mapCenter, point);
					if (distance < minimumDistance) {
						mostCentralPoint = point;
						mostCentralPointOffset = offset;
						minimumDistance = distance;
					}
				}
			}

			crosshair.transform.localPosition = mostCentralPoint;
			cameraService.Target = mostCentralPoint;
			//Debug.Log("point " + point);

			var zoomLevel = Mathf.Log(
				mostCentralPointOffset / cameraServiceSpec!.BaseDistance,
				cameraServiceSpec!.ZoomBase
			);
			cameraService.ZoomLevel = zoomLevel;
			//Debug.Log("zoomLevel " + zoomLevel);
		}
	}
}
