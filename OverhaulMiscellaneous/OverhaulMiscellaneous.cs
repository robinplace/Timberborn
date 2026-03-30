using UnityEngine;
using HarmonyLib;
using Timberborn.WaterSystemRendering;
using Timberborn.WaterSystemUI;
using Timberborn.UILayoutSystem;
using Timberborn.ModManagerScene;
using System.Runtime.InteropServices;
using System;
using Timberborn.GameSaveRepositorySystem;
using System.Reflection;
using Timberborn.StatusSystem;
using Timberborn.Emptying;
using Bindito.Core;
using Timberborn.SingletonSystem;
using Timberborn.CameraSystem;

public class OverhaulMiscellaneous : IModStarter {
	public void StartMod(IModEnvironment env) {
		Debug.Log(GetType().Name);
		var harmony = new Harmony("Robin.OverhaulMiscellaneous");
		harmony.PatchAll();

		Debug.Log(typeof(GameSaveRepository));
		Debug.Log(typeof(GameSaveRepository)
			.GetField(nameof(GameSaveRepository.ExperimentalSavesDir), BindingFlags.NonPublic | BindingFlags.Static));

		typeof(GameSaveRepository)
			.GetField(nameof(GameSaveRepository.ExperimentalSavesDir), BindingFlags.NonPublic | BindingFlags.Static)
			.SetValue(null, "Saves");

		Debug.Log($"runtime {RuntimeInformation.FrameworkDescription} environment {Environment.Version}");
	}
}

[Context("Game")]
[Context("MapEditor")]
class MiscellaneousConfigurator : IConfigurator {
	public void Configure(IContainerDefinition c) {
		Debug.Log(GetType().Name);
		c.Bind<Miscellaneous>().AsSingleton();
	}
}

class Miscellaneous(
	CameraService camera_service
) : ILoadableSingleton {
	public void Load() {
		camera_service._camera.fieldOfView = Mathf.Max(70f, camera_service._camera.fieldOfView);
	}
}

[HarmonyPatch]
class MiscellaneousPatch {
	// allow water toggle only explicitly i.e. from the panel & its keybind
	[HarmonyPrefix, HarmonyPatch(typeof(WaterOpacityToggle), nameof(WaterOpacityToggle.HideWater))]
	static bool HideWater() {
		return new System.Diagnostics.StackFrame(2).GetMethod().Name == nameof(WaterOpacityTogglePanel.OnWaterToggled);
	}

	// allow water toggle only explicitly i.e. from the panel & its keybind
	[HarmonyPrefix, HarmonyPatch(typeof(WaterOpacityToggle), nameof(WaterOpacityToggle.ShowWater))]
	static bool ShowWater() {
		return new System.Diagnostics.StackFrame(2).GetMethod().Name == nameof(WaterOpacityTogglePanel.OnWaterToggled);
	}

	// turn off panel pause
	[HarmonyPrefix, HarmonyPatch(typeof(OverlayPanelSpeedLocker), nameof(OverlayPanelSpeedLocker.OnPanelShown))]
	static bool OnPanelShown() {
		return false;
	}

	// turn off panel unpause
	[HarmonyPrefix, HarmonyPatch(typeof(OverlayPanelSpeedLocker), nameof(OverlayPanelSpeedLocker.OnPanelHidden))]
	static bool OnPanelHidden() {
		return false;
	}

	// hide certain warnings
	[HarmonyPrefix, HarmonyPatch(typeof(Emptiable), nameof(Emptiable.Awake))]
	static bool IsStatusVisible(Emptiable __instance) {
		__instance._emptyStatusToggle = new StatusToggle(StatusSpecification.Create("Empty", __instance._loc.T(Emptiable.EmptyingInProgressLocKey)), isPriorityStatus: true);
		__instance.DisableComponent();
		return false;
	}
}
