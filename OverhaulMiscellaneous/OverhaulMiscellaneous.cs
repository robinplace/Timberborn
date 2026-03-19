using UnityEngine;
using HarmonyLib;
using Timberborn.WaterSystemRendering;
using Timberborn.WaterSystemUI;
using Timberborn.UILayoutSystem;
using Timberborn.ModManagerScene;
using System.Runtime.InteropServices;
using System;
using Timberborn.TickSystem;
using MonoMod.Cil;
using Mono.Cecil;
using System.Collections.Generic;
using MonoMod.Utils;

public class OverhaulMiscellaneous: IModStarter {
	public void StartMod(IModEnvironment env) {
		Debug.Log(GetType().Name);
		var harmony = new Harmony("Robin.OverhaulMiscellaneous");
		harmony.PatchAll();

		Debug.Log($"runtime {RuntimeInformation.FrameworkDescription} environment {Environment.Version}");

		var module = ModuleDefinition.ReadModule(typeof(Ticker).Assembly.Location);
		var entry = module.GetType(typeof(Ticker).FullName).FindMethod(nameof(Ticker.TickOnce));
		Recurse(entry!);
	}

	readonly HashSet<MethodDefinition> method_set = [];

	private void Recurse(MethodDefinition entry) {
		if (method_set.Add(entry)) {
			Debug.Log($"recurse method {entry.DeclaringType.FullName}::{entry.Name}");
		} else {
			Debug.Log($"repeat method {entry.DeclaringType.FullName}::{entry.Name}");
			return;
		}
		var il = new ILContext(entry);
		var cur = new ILCursor(il).Goto(0);
		Debug.Log($"scan method {entry.DeclaringType.FullName}::{entry.Name}");
		while (true) {
			FieldReference? field_ref = null;
			MethodReference? method_ref = null;
			if (!cur.TryGotoNext(i => (
				i.MatchStfld(out field_ref) ||
				i.MatchCallvirt(out method_ref)
			))) break;
			if (field_ref != null) {
				var field = field_ref.Resolve();
				Debug.Log($"set field {field.DeclaringType.FullName}::{field.Name}");
			}
			if (method_ref != null) {
				var method = method_ref.Resolve();
				Debug.Log($"call virt {method.DeclaringType.FullName}::{method.Name}");
				Debug.Log($"body {method.HasBody}");
				Recurse(method);
			}
		}
	}

/*
		if (method_set.Add(method_ref)) {
			Debug.Log($"hook method {method.DeclaringType.FullName}::{method.Name}");
			hook_set.Add(new ILHook(
				method,
				RecursiveManipulate
			));
		} else {
			Debug.Log($"repeat method {method.DeclaringType.FullName}::{method.Name}");
		}
	readonly HashSet<ILHook> hook_set = [];

	private void RecursiveManipulate(ILContext il) {
		var cur = new ILCursor(il).Goto(0);
		while (true) {
			FieldReference? field_ref = null;
			MethodReference? method_ref = null;
			if (!cur.TryGotoNext(i => (
				i.MatchStfld(out field_ref) ||
				i.MatchCallvirt(out method_ref)
			))) break;
			if (field_ref != null) {
				var field = field_ref.ResolveReflection();
				Debug.Log($"set field {field.DeclaringType.FullName}::{field.Name}");
			}
			if (method_ref != null) {
				var method = method_ref.ResolveReflection();
				Debug.Log($"call virt {method.DeclaringType.FullName}::{method.Name}");
				RecurseHook(method);
			}
		}
	}*/
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
}
