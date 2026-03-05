using UnityEngine;
using HarmonyLib;
using Timberborn.OptionsGame;
using Timberborn.ApplicationLifetime;
using Timberborn.MainMenuScene;
using Timberborn.ModManagerScene;

public class OverhaulQuickstart: IModStarter {
	public void StartMod(IModEnvironment env) {
		Debug.Log(GetType().Name);
		var harmony = new Harmony("Robin.OverhaulQuickstart");
		harmony.PatchAll();
	}
}

[HarmonyPatch]
class QuickstartPatch {
	// turn off welcome screen
	[HarmonyPrefix, HarmonyPatch(typeof(MainMenuInitializer), nameof(MainMenuInitializer.ShowWelcomeScreen))]
	static bool ShowWelcomeScreen(MainMenuInitializer __instance) {
		__instance.ShowMainMenuPanel();
		//__instance._mainMenuPanel.LoadMostRecentSave();
		return false;
	}

	// turn off goodbye on quit to main menu
	[HarmonyPrefix, HarmonyPatch(typeof(GameOptionsBox), nameof(GameOptionsBox.ExitToMenuClicked))]
	static bool ExitToMenuClicked(GameOptionsBox __instance) {
		__instance._goodbyeBoxFactory._mainMenuSceneLoader.SaveAndOpenMainMenu();
		return false;
	}

	// turn off goodbye on quit to desktop
	[HarmonyPrefix, HarmonyPatch(typeof(GameOptionsBox), nameof(GameOptionsBox.ExitToDesktopClicked))]
	static bool ExitToDesktopClicked() {
		GameQuitter.Quit();
		return false;
	}

}
